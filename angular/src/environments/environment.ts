import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: baseUrl + '/',
  redirectUri: baseUrl,
  postLogoutRedirectUri: baseUrl,
  clientId: 'KnowledgeHub_App',
  responseType: 'code',
  scope: 'offline_access KnowledgeHub',
  requireHttps: false,
};

export const environment = {
  production: false,
  application: {
    baseUrl,
    name: '易课通知识库系统',
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
