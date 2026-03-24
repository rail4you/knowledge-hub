import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44305/',
  redirectUri: baseUrl,
  clientId: 'KnowledgeHub_App',
  responseType: 'code',
  scope: 'offline_access KnowledgeHub',
  requireHttps: true,
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
      url: 'https://localhost:44305',
      rootNamespace: 'KnowledgeHub',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;
