import { Injectable, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { Params } from '@angular/router';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { OAuthStorage } from 'angular-oauth2-oidc';
import { AbpOAuthService } from '@abp/ng.oauth';
import type { LoginParams } from '@abp/ng.core';

/**
 * 自定义 AuthService：修复登录/登出切换账号时的会话残留问题。
 *
 * Bug 背景 1（__host_login）：
 *   1. 用户访问 /admin-login 时，前端 LoginComponent 会写入 `__host_login=1` cookie，
 *      告知后端 Login.cshtml 渲染宿主登录表单；
 *   2. 正常登录流程里，登录成功时（后端 KnowledgeHubLoginModel.OnPostAsync）
 *      已经会清除这个 cookie；
 *   3. 但用户可能「仅访问了 /admin-login 但未提交」或「在 LeptonX 用户菜单登出」，
 *      此时 cookie 不会被清除。下次从首页点击登录时，后端会再次进入宿主模式，
 *      反复显示系统管理员登录页。
 *
 * Bug 背景 2（__tenant —— 登出后 /admin-login 变租户页）：
 *   后端 Razor 登录页是否渲染宿主表单，只看 `__host_login=1 且无 __tenant cookie`
 *   （Login.cshtml）。登出时若不清除 `__tenant`，下一次走授权码流程进入
 *   /Account/Login 时后端仍解析出旧租户 → 显示租户登录页，且 `admin` 在租户
 *   上下文会被后端直接拦截，导致宿主管理员再也进不去。
 *   因此登出 = 忘记租户，两个 cookie 都必须清除。
 *
 * Bug 背景 3（授权码回调被上一账号的过期 token 破坏）：
 *   ABP 的 `AuthCodeFlowStrategy.init()` 会**先**执行 `checkRememberMeOption()`，
 *   此时 discovery 文档尚未加载（`logoutUrl` 还是 undefined）。若 OAuth 存储里
 *   仍残留上一账号的**已过期** access_token（`MemoryTokenStorageService` 通过
 *   SharedWorker/localStorage 跨页残留，多标签页或长时间挂起后尤为常见），
 *   则会调用 `oAuthService.logOut()`：
 *     - 由于 `logoutUrl` 为空，它不会跳转 end-session，
 *     - 但会删除本次登录刚写入的 `PKCE_verifier` 和 token；
 *   之后 `tryLogin()` 换取新 token 时因缺少 PKCE 校验而静默失败，
 *   浏览器停留在 `/?iss=...&culture=...`（`code` 已被清除），
 *   用户必须再点一次登录才能进入系统。
 *
 *   修复：在授权码回调（URL 带 `code`）且 ABP 认证初始化之前，主动清空残留的
 *   access_token/id_token/refresh_token/expires_at 等**token 类**状态，
 *   但保留本次流程的 `nonce` / `PKCE_verifier`，使 `checkRememberMeOption()`
 *   不再误判并销毁 PKCE，保证一次登录成功。
 *
 * 此服务在 logout() / login() / navigateToLogin() / init() 触发时同步处理这些
 * 状态，作为纵深防御，覆盖所有登出与登录入口。
 */
@Injectable()
export class CustomAuthService extends AbpOAuthService {
  // 注意：不能用 'document' 命名——父类 AbpOAuthService 已声明为 private。
  // 用 'doc' 避免与父类同名属性冲突。
  private readonly doc = inject(DOCUMENT);
  private readonly oAuthStorage = inject(OAuthStorage);

  /**
   * token 类存储键：这些键表示「上一会话的身份」，新登录回调开始前必须清空。
   * 不能清 `nonce` / `PKCE_verifier`——它们是本次授权码流程换取 token 的必需品。
   */
  private static readonly STALE_TOKEN_KEYS = [
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
    // 登录前清理残留 token（不改动 nonce/PKCE，稍后 initCodeFlow 会重新生成）。
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
    for (const key of CustomAuthService.STALE_TOKEN_KEYS) {
      try {
        this.oAuthStorage.removeItem(key);
      } catch {
        // 隐私模式/跨域等场景写入 storage 可能抛错，不能因此阻断登录流程。
      }
    }
  }

  private clearLoginChannelCookies(): void {
    try {
      this.doc.cookie =
        '__host_login=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
      // 登出即忘记租户：残留 __tenant 会让下一次 /Account/Login（Razor）
      // 仍解析出旧租户，宿主管理员被挡在租户登录页之外。
      this.doc.cookie =
        '__tenant=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
    } catch {
      // 跨域或隐私模式下写入 cookie 可能抛错，登出流程不应因此失败。
    }
  }
}
