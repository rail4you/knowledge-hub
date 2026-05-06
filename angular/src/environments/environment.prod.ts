import { Environment } from '@abp/ng.core';

const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'https://localhost';

const oAuthConfig = {
  issuer: baseUrl,
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
    name: '易课通资源库系统',
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
