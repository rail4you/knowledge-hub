import { Injectable, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { Params } from '@angular/router';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { OAuthStorage } from 'angular-oauth2-oidc';
import { AbpOAuthService } from '@abp/ng.oauth';
import type { LoginParams } from '@abp/ng.core';

/**
 * token 类存储键：这些键表示「上一会话的身份」，新登录/登出时必须清空。
 * 不能清 `nonce` / `PKCE_verifier`——它们是授权码流程换取 token 的必需品。
 */
export const STALE_OAUTH_TOKEN_KEYS = [
  'access_token',
  'id_token',
  'refresh_token',
  'expires_at',
  'id_token_claims_obj',
  'id_token_expires_at',
  'id_token_stored_at',
  'access_token_stored_at',
  'granted_scopes',
  'session_state',
];

/**
 * 自定义 AuthService：登录/登出时的会话清理。
 *
 * 当前为纯 Angular 密码授权（oAuthConfig.responseType='password'）：
 * 凭据直接 POST /connect/token，不经过后端 Razor 登录页。
 *
 * - logout()：先清登录通道 cookie（`__host_login` 历史残留、`__tenant`），
 *   再走父类 revokeToken + 刷新应用状态 + 回首页。登出即忘记租户，
 *   下次登录页按无租户处理（自动选中首个租户），避免旧租户串扰。
 * - login() / navigateToLogin()：先清残留身份 token，避免上一账号的过期
 *   token 干扰本次登录。
 * - init()：保留授权码回调的陈旧 token 清理作为纵深防御（当前配置下不会走到，
 *   若将来切回 code 流程仍可生效；清理时保留 nonce / PKCE_verifier）。
 */
@Injectable()
export class CustomAuthService extends AbpOAuthService {
  // 注意：不能用 'document' 命名——父类 AbpOAuthService 已声明为 private。
  // 用 'doc' 避免与父类同名属性冲突。
  private readonly doc = inject(DOCUMENT);
  private readonly oAuthStorage = inject(OAuthStorage);

  override async init(): Promise<any> {
    // 授权码回调：先丢弃上一账号残留的 token，避免 ABP 的 checkRememberMeOption()
    // 在 discovery 加载前调用 logOut() 误删本次流程的 PKCE_verifier，导致换取 token 失败。
    //
    // ABP 的 MemoryTokenStorageService 通过 SharedWorker 异步回填 token，
    // 构造时会 postMessage('get')，其响应可能晚于本次同步清理到达并重新写回缓存，
    // 因此这里清理两次：一次立即，一次让出一个宏任务、等异步回填落地后再清。
    if (this.isAuthorizationCodeCallback()) {
      this.clearStaleOAuthTokens();
      await new Promise(resolve => setTimeout(resolve, 0));
      this.clearStaleOAuthTokens();
    }
    return super.init();
  }

  override logout(queryParams?: Params): Observable<any> {
    // 必须在 super.logout() 之前清除：logout 内部会触发 window.location 跳转去
    // OAuth end-session endpoint，跳转后当前页面 JS 上下文就不可用了。
    this.clearLoginChannelCookies();
    return super.logout(queryParams).pipe(
      // 兜底：即使 logout 内部走非跳转分支（如 SSR），subscribe 时也确保 cookie 被清。
      tap(() => this.clearLoginChannelCookies()),
    );
  }

  override navigateToLogin(queryParams?: Params): void {
    // 登录前清理残留 token（密码授权下 token 请求不依赖旧 token，可安全清理）。
    this.clearStaleOAuthTokens();
    super.navigateToLogin(queryParams);
  }

  override login(params: LoginParams): Observable<any> {
    this.clearStaleOAuthTokens();
    return super.login(params);
  }

  /** 当前 URL 是否为 OAuth 授权码回调（后端重定向回 redirectUri 时带 code/error）。 */
  private isAuthorizationCodeCallback(): boolean {
    if (typeof window === 'undefined' || !window.location) {
      return false;
    }
    try {
      const params = new URLSearchParams(window.location.search);
      return params.has('code') || params.has('error');
    } catch {
      return false;
    }
  }

  /** 清空残留的身份 token（保留 nonce/PKCE_verifier）。 */
  private clearStaleOAuthTokens(): void {
    for (const key of STALE_OAUTH_TOKEN_KEYS) {
      try {
        this.oAuthStorage.removeItem(key);
      } catch {
        // 隐私模式/跨域等场景写入 storage 可能抛错，不能因此阻断登录流程。
      }
    }
  }

  private clearLoginChannelCookies(): void {
    try {
      // 历史残留的宿主标记（Razor 时代产物，无人再写，防残留串扰）与租户选择。
      this.doc.cookie =
        '__host_login=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
      this.doc.cookie =
        '__tenant=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
    } catch {
      // 跨域或隐私模式下写入 cookie 可能抛错，登出流程不应因此失败。
    }
  }
}
