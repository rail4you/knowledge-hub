// 搜索专项：压 Meilisearch 检索链路（POST /api/app/search/search 与 hybrid-search）。
//
// 运行：
//   k6 run --env BASE=https://localhost:44305 --env USER=admin --env PASS='1q2w3E*' scripts/perf/k6/search.js
import http from 'k6/http';
import { sleep } from 'k6';
import { login, baseParamsWithAuth, randomQuery, BASE } from './lib.js';

export const options = {
  stages: [
    { duration: '1m', target: 20 },
    { duration: '3m', target: 60 },
    { duration: '3m', target: 120 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.02'],
    http_req_duration: ['p(95)<800', 'p(99)<1500'],
  },
};

export function setup() {
  return { token: login() };
}

export default function (data) {
  const p = baseParamsWithAuth(data.token);
  const body = JSON.stringify({ query: randomQuery(), skipCount: 0, maxResultCount: 12 });

  http.post(`${BASE}/api/app/search/search`, body, {
    ...p,
    headers: { ...p.headers, 'Content-Type': 'application/json' },
    tags: { scenario: 'search' },
  });

  if (Math.random() < 0.3) {
    http.post(`${BASE}/api/app/search/hybrid-search`, body, {
      ...p,
      headers: { ...p.headers, 'Content-Type': 'application/json' },
      tags: { scenario: 'search' },
    });
  }

  sleep(Math.random());
}
