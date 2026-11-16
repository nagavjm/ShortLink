# ShortLink Load Testing Suite with k6

This directory contains production-grade **k6** load testing scripts engineered to measure throughput, latency percentiles (p95/p99), Redis cache efficiency, and concurrency limits for the **ShortLink** platform.

---

## 1. Prerequisites & Installation

To run these tests on Windows, install the `k6` CLI:

### Option A: Using Windows Package Manager (winget)
```powershell
winget install k6 --source winget
```

### Option B: Using Chocolatey
```powershell
choco install k6
```

### Option C: Direct Download
Download the official standalone binary from [https://k6.io/docs/get-started/installation/](https://k6.io/docs/get-started/installation/).

Verify installation:
```powershell
k6 version
```

---

## 2. Test Scripts Overview

| Script Name | Purpose | Target Endpoint | Concurrency / Load Pattern |
| :--- | :--- | :--- | :--- |
| `url-creation.js` | Measures write throughput, DB inserts, and Base62 generation. | `POST /api/urls` | Ramps 1 $\rightarrow$ 50 VUs over 60s |
| `url-redirection.js` | Measures HTTP 302 redirect throughput and header integrity. | `GET /{shortCode}` | Ramps 5 $\rightarrow$ 200 VUs over 60s |
| `cache-hit-benchmark.js` | Benchmarks maximum read capacity with 100% Redis cache hits. | `GET /{shortCode}` | Constant 1,000 RPS sustained for 30s |
| `cache-miss-benchmark.js` | Evaluates negative caching and protection under penetration scans. | `GET /fakeCode_X` | Ramps 10 $\rightarrow$ 100 VUs over 40s |
| `stress-test.js` | Comprehensive stress test: 85% reads + 10% writes + 5% list queries. | Blended routes | Ramps 10 $\rightarrow$ 500 VUs over 3m30s |

---

## 3. Running the Tests

Ensure the ASP.NET Core backend is running:
```powershell
dotnet run --project src/ShortLink.API --urls "http://localhost:5000"
```

### 1. URL Creation Benchmark
```powershell
k6 run load-tests/url-creation.js
```

### 2. URL Redirection (HTTP 302) Benchmark
```powershell
k6 run load-tests/url-redirection.js
```

### 3. Redis Cache Hit Benchmark (1,000 RPS)
```powershell
k6 run load-tests/cache-hit-benchmark.js
```

### 4. Cache Miss & Negative Caching Benchmark
```powershell
k6 run load-tests/cache-miss-benchmark.js
```

### 5. Full Stress Test (500 Concurrent Users)
```powershell
k6 run load-tests/stress-test.js
```

> **Tip**: To target a custom URL or staging server, pass the `TARGET_URL` environment variable:
> ```powershell
> k6 run -e TARGET_URL=http://staging-server:5000 load-tests/stress-test.js
> ```

---

## 4. Key Metrics to Measure

- **Requests Per Second (RPS)**: Measured by `http_reqs`.
- **Latency Percentiles**:
  - `p(95)`: The maximum response time for 95% of users (SLA target: $< 30\text{ms}$ for redirects).
  - `p(99)`: The maximum response time for 99% of users (SLA target: $< 80\text{ms}$ for redirects).
- **Error Rate**: Measured by `http_req_failed` (target: $< 0.1\%$).
- **HTTP 429 Frequency**: Measured by `stress_rate_limit_hits_429` (proves rate limiting is actively enforcing quotas).
