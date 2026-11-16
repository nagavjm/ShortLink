import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Counter, Rate } from 'k6/metrics';

// Comprehensive telemetry
const redirectDuration = new Trend('stress_redirect_duration_ms');
const creationDuration = new Trend('stress_creation_duration_ms');
const rateLimitHitCounter = new Counter('stress_rate_limit_hits_429');
const successfulRequests = new Counter('stress_successful_ops_total');
const errorRate = new Rate('stress_unhandled_error_rate');

export const options = {
  scenarios: {
    stress_test: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '30s', target: 50 },   // Warm-up
        { duration: '1m',  target: 150 },  // Normal peak traffic
        { duration: '1m',  target: 350 },  // High stress traffic
        { duration: '30s', target: 500 },  // Extreme spike (saturation threshold)
        { duration: '30s', target: 0 },    // Cooldown
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    'stress_redirect_duration_ms': ['p(95)<50', 'p(99)<120'], // Fast redirect SLA
    'stress_unhandled_error_rate': ['rate<0.02'],              // <2% unhandled 5xx errors
  },
};

const BASE_URL = __ENV.TARGET_URL || 'http://localhost:5000';

export function setup() {
  // Pre-seed a batch of 10 URLs for concurrent read testing
  const seedCodes = [];
  for (let i = 1; i <= 10; i++) {
    const payload = JSON.stringify({
      originalUrl: `https://www.example.com/stress-test/campaign-${i}`,
    });

    const res = http.post(`${BASE_URL}/api/urls`, payload, {
      headers: { 'Content-Type': 'application/json' },
    });

    if (res.status === 201) {
      const data = JSON.parse(res.body);
      seedCodes.push(data.shortCode);
    }
  }

  return { seedCodes: seedCodes.length > 0 ? seedCodes : ['000001'] };
}

export default function (data) {
  const roll = Math.random();

  if (roll < 0.85) {
    // 85% Traffic: Read / Redirect Path (Hot Path)
    const codeIndex = Math.floor(Math.random() * data.seedCodes.length);
    const shortCode = data.seedCodes[codeIndex];

    const res = http.get(`${BASE_URL}/${shortCode}`, { redirects: 0 });
    redirectDuration.add(res.timings.duration);

    const ok = check(res, {
      'redirect is 302 Found': (r) => r.status === 302,
    });

    if (ok) {
      successfulRequests.add(1);
      errorRate.add(0);
    } else {
      errorRate.add(1);
    }
  } else if (roll < 0.95) {
    // 10% Traffic: Write / URL Creation Path
    const rnd = Math.floor(Math.random() * 100000);
    const payload = JSON.stringify({
      originalUrl: `https://example.org/landing-page/${rnd}`,
    });

    const params = {
      headers: {
        'Content-Type': 'application/json',
        'X-Forwarded-For': `172.16.${Math.floor(__VU / 255)}.${__VU % 255 + 1}`,
      },
    };

    const res = http.post(`${BASE_URL}/api/urls`, payload, params);
    creationDuration.add(res.timings.duration);

    if (res.status === 201) {
      successfulRequests.add(1);
      errorRate.add(0);
    } else if (res.status === 429) {
      // Expected when client IP exceeds 100 req/min
      rateLimitHitCounter.add(1);
      errorRate.add(0); // 429 is expected rate limiting, not a server crash
    } else {
      errorRate.add(1);
    }
  } else {
    // 5% Traffic: Directory / Pagination
    const res = http.get(`${BASE_URL}/api/urls?pageNumber=1&pageSize=10`);
    check(res, {
      'paged urls returns 200': (r) => r.status === 200,
    });
  }

  sleep(0.05); // Rapid iteration
}
