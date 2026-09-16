import { Environment } from '@abp/ng.core';

const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'https://localhost';

const oAuthConfig = {
  issuer: baseUrl,
  redirectUri: baseUrl,
  postLogoutRedirectUri: baseUrl,
  clientId: 'KnowledgeHub_App',
  // 与开发环境一致：SPA 自研登录页走密码授权，见 environment.ts 注释。
  responseType: 'password',
  // 与远端 /getEnvConfig（dynamic-env.json）保持一致的 scope，避免远端拉取失败时缺少
  // openid/profile 等声明。
  scope: 'offline_access openid profile email phone KnowledgeHub',
  requireHttps: false,
};

export const environment = {
  production: true,
  application: {
    baseUrl,
    name: '易课通资源库系统',
    maxFileSizeBytes: 500 * 1024 * 1024,
  },
  oAuthConfig,
  apis: {
    default: {
      url: baseUrl,
      rootNamespace: 'KnowledgeHub',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
  remoteEnv: {
    url: '/getEnvConfig',
    mergeStrategy: 'deepmerge'
  }
} as Environment;
