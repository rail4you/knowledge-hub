// AI 问答专项（SSE）。注意：受外部 Qwen 限流与费用影响，务必小并发单独跑。
//
// 运行：
//   k6 run --env BASE=https://localhost:44305 --env USER=admin --env PASS='1q2w3E*' \
//     --env VUS=5 --env DURATION=3m scripts/perf/k6/ai.js
import http from 'k6/http';
import { sleep } from 'k6';
import { login, baseParamsWithAuth, BASE } from './lib.js';

const VUS = Number(__ENV.VUS || 5);
const DURATION = __ENV.DURATION || '3m';

export const options = {
  vus: VUS,
  duration: DURATION,
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<15000'],
  },
};

export function setup() {
  return { token: login() };
}

export default function (data) {
  const p = baseParamsWithAuth(data.token);
  const payload = JSON.stringify({ message: '用一句话介绍这个平台' });

  http.post(`${BASE}/api/learning/ai/chat`, payload, {
    ...p,
    headers: { ...p.headers, 'Content-Type': 'application/json' },
    timeout: '60s',
    tags: { scenario: 'ai-chat' },
  });

  sleep(3 + Math.random() * 3);
}
