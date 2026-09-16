import { Injectable, inject } from '@angular/core';
import { OAuthStorage } from 'angular-oauth2-oidc';
import { SessionStateService } from '@abp/ng.core';

/**
 * 认证 fetch 工具：集中为手写 fetch() 附带访问令牌与租户/语言头。
 *
 * 背景：登录改为纯 Angular 密码授权（password grant）后，访问令牌只保存在内存
 * （ABP 的 MemoryTokenStorageService），浏览器不再持有认证 cookie。
 * 生成的代理（RestService）由 ABP 的 OAuthApiInterceptor 自动附带令牌，无需处理；
 * 但手写 fetch() 过去只依赖 `credentials: 'include'` 携带 cookie，现在会 302 跳登录页，
 * 表现为：AI 中心资源列表为空、聊天/语音/特教生成失败、资源预览与下载报错。
 *
 * 这里复刻 OAuthApiInterceptor 的请求头策略，避免各调用点重复实现。
 * 需在 Angular 注入上下文（组件/服务的构造函数或字段初始化）中 inject 后使用。
 */
@Injectable({ providedIn: 'root' })
export class AuthFetchService {
  private readonly storage = inject(OAuthStorage);
  private readonly sessionState = inject(SessionStateService);

  /** 当前访问令牌（未登录返回 null）。 */
  getAccessToken(): string | null {
    try {
      return this.storage.getItem('access_token');
    } catch {
      return null;
    }
  }

  /** 构造带认证信息的请求头（不覆盖调用方显式设置的同名头）。 */
  buildHeaders(extra?: HeadersInit): Headers {
    const headers = new Headers(extra);
    const token = this.getAccessToken();
    if (token && !headers.has('Authorization')) {
      headers.set('Authorization', `Bearer ${token}`);
    }
    const lang = this.sessionState.getLanguage();
    if (lang && !headers.has('Accept-Language')) {
      headers.set('Accept-Language', lang);
    }
    const tenant = this.sessionState.getTenant();
    if (tenant?.id && !headers.has('__tenant')) {
      headers.set('__tenant', tenant.id);
    }
    return headers;
  }

  /** 与原生 fetch 同签名，自动附带认证头与 credentials。 */
  fetch(url: string, init: RequestInit = {}): Promise<Response> {
    return fetch(url, {
      ...init,
      headers: this.buildHeaders(init.headers),
      credentials: 'include',
    });
  }

  /**
   * 为浏览器原生发起的 URL（<a download> / <img> / <video> / <embed> 等无法设置
   * 请求头）追加 access_token 查询参数，配合后端 GrantAllPoliciesMiddleware 放行。
   */
  withAccessToken(url: string): string {
    const token = this.getAccessToken();
    if (!token) return url;
    const separator = url.includes('?') ? '&' : '?';
    return `${url}${separator}access_token=${encodeURIComponent(token)}`;
  }
}
