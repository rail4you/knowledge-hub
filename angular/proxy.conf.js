const PROXY_TARGET = 'https://localhost:44305';

/**
 * 所有反代到 API 的路径共用这两个头：
 *
 * ① `changeOrigin: false` —— 保留浏览器原始 Host。
 *    域名绑定多租户要求后端按 Host 解析学校域名（如 qidi.localhost → qidi 租户），
 *    若改写成 localhost:44305，域名解析会失效。
 *
 * ② `X-Forwarded-Proto: http` —— 告诉后端「浏览器侧是 http」。
 *    API 用 UseForwardedHeaders(XForwardedProto) 还原对外协议，OpenIddict 据此推导 issuer。
 *    若 /api 与 /connect 两条路径推导出的 issuer 不一致（一个 http 一个 https），
 *    令牌校验会因 iss 不符而 401 —— 表现为「登录后立刻掉线」。
 */
const API_HEADERS = { 'X-Forwarded-Proto': 'http' };

const proxyConfig = {
  '/api': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/Account': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/.well-known': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/AbpApplicationConfiguration': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/connect': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/signin-oidc': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/signout-callback-oidc': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/libs': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/Themes': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/Pages': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/global-styles.css': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/global-scripts.js': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/dev-login-helper.js': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  '/favicon.svg': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
  // 仿真实训 WASM 本地镜像：开发模式下把 /wasm/ 反向代理给 API 容器，
  // API 用 Kestrel 兜底静态托管（生产场景下 Nginx 直接静态托管）。
  // 配置在 /api 之后：避免匹配过宽。
  // 注意：必须用 /wasm/（含尾部斜杠）避免误拦截 /wasm-management 等 Angular 路由。
  '/wasm/': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
    logLevel: 'warn',
  },
  // 直播 WebSocket 代理：通过 Angular dev server (HTTP/1.1) 转发到后端，
  // 避免浏览器直接连接 wss:// Kestrel 时 HTTP/2 导致 WebSocket 升级失败
  '/api/recruitment-live/ws': {
    target: 'wss://localhost:44305',
    ws: true,
    secure: false,
    changeOrigin: false,
    headers: { ...API_HEADERS },
  },
};

module.exports = proxyConfig;
