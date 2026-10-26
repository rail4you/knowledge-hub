import { Environment } from '@abp/ng.core';

const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'https://localhost';

const oAuthConfig = {
  // issuer 必须与后端 OpenIddict discovery 的 issuer（带结尾斜杠）完全一致，
  // 否则 angular-oauth2-oidc 会报 "invalid issuer in discovery document"。
  // 用 window.location.origin 而非固定域名：同一份构建产物可服务任意域名
  //（IP / localhost / 学校自有域名），dynamic-env.json 不再写死绝对地址。
  issuer: baseUrl + '/',
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
