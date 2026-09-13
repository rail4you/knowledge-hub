// 混合读写场景：列表 / 分类 / 专业 / 搜索，贴近管理端+门户的日常访问分布。
//
// 运行：
//   k6 run --env BASE=https://localhost:44305 --env USER=admin --env PASS='1q2w3E*' scripts/perf/k6/mixed.js
//   k6 run --out json=reports/k6-mixed.json scripts/perf/k6/mixed.js
import http from 'k6/http';
import { sleep, group } from 'k6';
import { login, baseParamsWithAuth, randomQuery, BASE } from './lib.js';

export const options = {
  stages: [
    { duration: '1m', target: 50 },
    { duration: '3m', target: 100 },
    { duration: '3m', target: 200 },
    { duration: '2m', target: 300 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{scenario:list}': ['p(95)<300'],
    'http_req_duration{scenario:categories}': ['p(95)<300'],
    'http_req_duration{scenario:search}': ['p(95)<800'],
  },
};

export function setup() {
  return { token: login() };
}

export default function (data) {
  const p = baseParamsWithAuth(data.token);

  group('list', () => {
    http.get(`${BASE}/api/app/resource/filtered-list?skipCount=0&maxResultCount=20`, {
      ...p,
      tags: { scenario: 'list' },
    });
  });

  group('categories', () => {
    http.get(`${BASE}/api/app/resource/categories`, {
      ...p,
      tags: { scenario: 'categories' },
    });
  });

  if (Math.random() < 0.4) {
    group('search', () => {
      http.post(
        `${BASE}/api/app/search/search`,
        JSON.stringify({ query: randomQuery(), skipCount: 0, maxResultCount: 12 }),
        { ...p, headers: { ...p.headers, 'Content-Type': 'application/json' }, tags: { scenario: 'search' } },
      );
    });
  }

  sleep(Math.random() * 1.5);
}
