const PROXY_TARGET = 'https://localhost:44305';

const proxyConfig = {
  '/api': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/Account': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/.well-known': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: {
      'X-Forwarded-Proto': 'http',
    },
  },
  '/AbpApplicationConfiguration': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/connect': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: false,
    headers: {
      'X-Forwarded-Proto': 'http',
    },
  },
  '/signin-oidc': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/signout-callback-oidc': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/libs': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/Themes': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/Pages': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/global-styles.css': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/global-scripts.js': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/dev-login-helper.js': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  '/favicon.svg': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
  },
  // 仿真实训 WASM 本地镜像：开发模式下把 /wasm/ 反向代理给 API 容器，
  // API 用 Kestrel 兜底静态托管（生产场景下 Nginx 直接静态托管）。
  // 配置在 /api 之后：避免匹配过宽。
  // 注意：必须用 /wasm/（含尾部斜杠）避免误拦截 /wasm-management 等 Angular 路由。
  '/wasm/': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
    logLevel: 'warn',
  },
  // 直播 WebSocket 代理：通过 Angular dev server (HTTP/1.1) 转发到后端，
  // 避免浏览器直接连接 wss:// Kestrel 时 HTTP/2 导致 WebSocket 升级失败
  '/api/recruitment-live/ws': {
    target: 'wss://localhost:44305',
    ws: true,
    secure: false,
    changeOrigin: true,
  },
};

module.exports = proxyConfig;
