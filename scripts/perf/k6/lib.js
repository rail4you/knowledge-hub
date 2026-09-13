import http from 'k6/http';
import { check } from 'k6';

export const BASE = __ENV.BASE || 'https://localhost:44305';
export const TENANT = __ENV.TENANT || '';
export const CLIENT_ID = __ENV.CLIENT_ID || 'KnowledgeHub_App';

// 开发自签名证书需要跳过校验；生产环境请置 false。
export const INSECURE = (__ENV.INSECURE || 'true') !== 'false';

function baseParams(extra = {}) {
  return { insecureSkipTLSVerify: INSECURE, ...extra };
}

export function login(params = {}) {
  const username = params.username || __ENV.USER || 'admin';
  const password = params.password || __ENV.PASS || '1q2w3E*';
  const tenant = params.tenant ?? TENANT;

  const headers = { 'Content-Type': 'application/x-www-form-urlencoded' };
  if (tenant) headers['__tenant'] = tenant;

  const res = http.post(
    `${BASE}/connect/token`,
    `grant_type=password&client_id=${CLIENT_ID}` +
      `&username=${encodeURIComponent(username)}` +
      `&password=${encodeURIComponent(password)}` +
      `&scope=KnowledgeHub offline_access`,
    baseParams({ headers, tags: { scenario: 'login' } }),
  );

  check(res, { 'login 200': (r) => r.status === 200 });
  return res.json('access_token');
}

export function authHeaders(token, tenant = TENANT, extra = {}) {
  const headers = { Authorization: `Bearer ${token}`, ...extra };
  if (tenant) headers['__tenant'] = tenant;
  return headers;
}

export function baseParamsWithAuth(token, tenant = TENANT, extra = {}) {
  return baseParams({ headers: authHeaders(token, tenant), ...extra });
}

export const QUERIES = ['课程', '教学', '实训', 'Python', '数据分析', '英语', '实习'];
export function randomQuery() {
  return QUERIES[Math.floor(Math.random() * QUERIES.length)];
}
