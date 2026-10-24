import { Environment } from '@abp/ng.core';

/**
 * 开发环境同样使用「当前站点 origin」而不是硬编码 localhost。
 *
 * <p>原因：域名绑定多租户要求 API 请求携带浏览器的原始 Host（学校域名）。
 * 若 baseUrl 写死 http://localhost:4200，访问 http://qidi.localhost:4200 时
 * 所有 API 请求都会打到 localhost，后端解析不到学校域名，域名绑定在开发环境失效。
 * 生产环境（environment.prod.ts）本来就是 window.location.origin。</p>
 */
const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:4200';

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
