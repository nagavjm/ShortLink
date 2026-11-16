import http from 'k6/http';
import { check } from 'k6';
import { Trend, Counter } from 'k6/metrics';

// High-precision benchmark metrics
const cacheHitLatency = new Trend('cache_hit_latency_ms', true);
const cacheHitRequests = new Counter('cache_hit_requests_total');

export const options = {
  scenarios: {
    constant_rate_benchmark: {
      executor: 'constant-arrival-rate',
      rate: 1000,             // 1,000 requests per second
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 100,
      maxVUs: 250,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.005'],            // 99.5% success rate
    http_req_duration: ['p(95)<15', 'p(99)<35'], // Ultra-low sub-20ms Redis latency SLA
  },
};

const BASE_URL = __ENV.TARGET_URL || 'http://localhost:5000';

export function setup() {
  // Pre-seed an active key into Redis
  const payload = JSON.stringify({
    originalUrl: 'https://en.wikipedia.org/wiki/URL_shortening',
  });

  const res = http.post(`${BASE_URL}/api/urls`, payload, {
    headers: { 'Content-Type': 'application/json' },
  });

  if (res.status === 201) {
    const data = JSON.parse(res.body);
    // Warm Redis by performing one initial lookup
    http.get(`${BASE_URL}/${data.shortCode}`, { redirects: 0 });
    return { shortCode: data.shortCode };
  }

  return { shortCode: '000001' };
}

export default function (data) {
  const params = {
    redirects: 0, // Intercept HTTP 302 directly
  };

  const res = http.get(`${BASE_URL}/${data.shortCode}`, params);

  cacheHitLatency.add(res.timings.duration);
  cacheHitRequests.add(1);

  check(res, {
    'status is 302 Found': (r) => r.status === 302,
    'latency is under 20ms': (r) => r.timings.duration < 20,
  });
}
