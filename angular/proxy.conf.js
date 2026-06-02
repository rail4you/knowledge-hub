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
};

module.exports = proxyConfig;
