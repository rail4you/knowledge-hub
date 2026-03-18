import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost';

const oAuthConfig = {
  issuer: 'http://localhost',
  redirectUri: baseUrl,
  clientId: 'KnowledgeHub_App',
  responseType: 'code',
  scope: 'offline_access KnowledgeHub',
  requireHttps: false,
};

export const environment = {
  production: true,
  application: {
    baseUrl,
    name: 'KnowledgeHub',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'http://localhost',
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
