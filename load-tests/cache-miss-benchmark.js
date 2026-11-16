import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Counter, Rate } from 'k6/metrics';

// Metrics for cache miss analysis
const missLatency = new Trend('cache_miss_latency_ms');
const negativeCacheHits = new Counter('negative_cache_hits_total');
const missSuccessRate = new Rate('negative_cache_resolution_rate');

export const options = {
  scenarios: {
    penetration_attack_simulation: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '10s', target: 50 },  // Ramp to 50 concurrent attackers
        { duration: '20s', target: 100 }, // Peak 100 attackers querying non-existent codes
        { duration: '10s', target: 0 },   // Ramp-down
      ],
    },
  },
  thresholds: {
    // Expected response for non-existent codes is HTTP 404
    http_req_duration: ['p(95)<80', 'p(99)<180'],
    negative_cache_resolution_rate: ['rate>0.99'],
  },
};

const BASE_URL = __ENV.TARGET_URL || 'http://localhost:5000';

export default function () {
  // Query a pool of 20 distinct missing codes so negative caching takes effect
  const codeIndex = Math.floor(Math.random() * 20);
  const nonExistentCode = `fakeCode_${codeIndex}`;

  const res = http.get(`${BASE_URL}/${nonExistentCode}`, { redirects: 0 });

  missLatency.add(res.timings.duration);

  const isExpected404 = check(res, {
    'status is 404 Not Found': (r) => r.status === 404,
    'returns RFC 7807 problem json': (r) => r.headers['Content-Type'] && r.headers['Content-Type'].includes('problem+json'),
  });

  if (isExpected404) {
    negativeCacheHits.add(1);
    missSuccessRate.add(1);
  } else {
    missSuccessRate.add(0);
  }

  sleep(0.05);
}
