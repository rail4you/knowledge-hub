import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: baseUrl + '/',
  redirectUri: baseUrl,
  postLogoutRedirectUri: baseUrl,
  clientId: 'KnowledgeHub_App',
  // 必须用 password：自研 LoginComponent 直接提交用户名/密码走 /connect/token 密码授权。
  // 若用 code（授权码），AuthCodeFlowStrategy.login 会忽略凭据直接跳转后端，
  // 导致输错密码也无任何错误提示、登录后落点不可控。后端已为该 client 开通 Password 授权。
  responseType: 'password',
  scope: 'offline_access KnowledgeHub',
  requireHttps: false,
};

export const environment = {
  production: false,
  application: {
    baseUrl,
    name: '易课通知识库系统',
    maxFileSizeBytes: 500 * 1024 * 1024,
  },
  oAuthConfig,
  apis: {
    default: {
      url: baseUrl,
      rootNamespace: 'KnowledgeHub',
    },
    AbpAccountPublic: {
      url: baseUrl + '/',
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;
