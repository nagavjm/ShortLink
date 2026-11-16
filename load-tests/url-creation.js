import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';

// Custom Metrics for .NET Architect Analysis
const creationLatency = new Trend('url_creation_duration_ms');
const errorRate = new Rate('creation_error_rate');
const successfulCreations = new Counter('successful_creations_total');

export const options = {
  scenarios: {
    creation_load: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '15s', target: 20 },  // Ramp-up to 20 virtual users
        { duration: '30s', target: 50 },  // Steady load at 50 VUs
        { duration: '15s', target: 0 },   // Ramp-down
      ],
      gracefulRampDown: '5s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.02'],              // Less than 2% errors
    http_req_duration: ['p(95)<150', 'p(99)<300'], // p95 < 150ms, p99 < 300ms
    creation_error_rate: ['rate<0.02'],
  },
};

const BASE_URL = __ENV.TARGET_URL || 'http://localhost:5000';

export default function () {
  const randomSuffix = Math.floor(Math.random() * 1000000);
  const payload = JSON.stringify({
    originalUrl: `https://www.example.com/products/item-${randomSuffix}?campaign=k6-load-test`,
    expiresAt: new Date(Date.now() + 86400000 * 7).toISOString(), // 7 days in future
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      'X-Forwarded-For': `10.0.${Math.floor(__VU / 255)}.${__VU % 255 + 1}`, // Simulates unique client IPs
    },
  };

  const res = http.post(`${BASE_URL}/api/urls`, payload, params);

  creationLatency.add(res.timings.duration);

  const isSuccess = check(res, {
    'status is 201 Created': (r) => r.status === 201,
    'has shortUrl': (r) => {
      try {
        const json = JSON.parse(r.body);
        return json.shortUrl && json.shortCode;
      } catch (e) {
        return false;
      }
    },
  });

  if (isSuccess) {
    successfulCreations.add(1);
    errorRate.add(0);
  } else {
    errorRate.add(1);
  }

  sleep(0.1); // Small pacing delay between requests
}
