import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';

// Custom Metrics
const redirectLatency = new Trend('redirect_duration_ms');
const redirectSuccessRate = new Rate('redirect_success_rate');
const totalRedirects = new Counter('total_redirects_completed');

export const options = {
  scenarios: {
    redirect_load: {
      executor: 'ramping-vus',
      startVUs: 5,
      stages: [
        { duration: '15s', target: 50 },   // Warm up to 50 concurrent users
        { duration: '30s', target: 200 },  // High concurrency: 200 VUs
        { duration: '15s', target: 0 },    // Ramp-down
      ],
      gracefulRampDown: '5s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],              // Strict SLA: <1% failure
    http_req_duration: ['p(95)<30', 'p(99)<80'], // Sub-50ms redirect response
    redirect_success_rate: ['rate>0.99'],
  },
};

const BASE_URL = __ENV.TARGET_URL || 'http://localhost:5000';
let preCreatedShortCode = null;

// Setup phase: pre-creates a test URL to guarantee a valid redirect target
export function setup() {
  const payload = JSON.stringify({
    originalUrl: 'https://github.com/nagavjm/ShortLink',
  });

  const res = http.post(`${BASE_URL}/api/urls`, payload, {
    headers: { 'Content-Type': 'application/json' },
  });

  if (res.status === 201) {
    const data = JSON.parse(res.body);
    return { shortCode: data.shortCode };
  }

  return { shortCode: '000001' }; // Fallback code
}

export default function (data) {
  const shortCode = data.shortCode;

  // Set redirects to 0 so k6 inspects the HTTP 302 directly rather than auto-following
  const params = {
    redirects: 0,
    headers: {
      'Accept': 'text/html,application/xhtml+xml',
    },
  };

  const res = http.get(`${BASE_URL}/${shortCode}`, params);

  redirectLatency.add(res.timings.duration);

  const isSuccess = check(res, {
    'status is 302 Found': (r) => r.status === 302,
    'has Location header': (r) => r.headers['Location'] !== undefined,
    'has no-cache header': (r) => r.headers['Cache-Control'] && r.headers['Cache-Control'].includes('no-cache'),
  });

  if (isSuccess) {
    redirectSuccessRate.add(1);
    totalRedirects.add(1);
  } else {
    redirectSuccessRate.add(0);
  }

  sleep(0.05); // High throughput pacing
}
