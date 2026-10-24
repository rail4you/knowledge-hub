import {
  HttpContextToken,
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { OAuthService, OAuthStorage } from 'angular-oauth2-oidc';
import { Observable, catchError, from, switchMap, throwError, timer } from 'rxjs';
import { AuthErrorService } from './auth-error.service';
import { STALE_OAUTH_TOKEN_KEYS } from './custom-auth.service';

/**
 * 全局认证恢复拦截器：让「后端服务重启」对用户无感知。
 *
 * 背景：生产重启后，浏览器里残留的 access_token 可能已失效（签名密钥轮换、
 * 或 API 还没就绪），之前没有任何全局 401 处理——请求直接失败，各页面又各自
 * swallow 错误，于是课程等页面表现为 404/空白，必须手动多刷几次才能恢复。
 *
 * 本拦截器做三件事（全部幂等、最多重试一次）：
 * 1. 401 + 有 refresh_token → 单飞（去重并发）静默刷新一次，成功则带上新
 *    token 自动重放原请求；用户无感知，页面不会闪错误。
 * 2. 401 且无 refresh_token / 刷新失败 → 清掉残留 token，弹全局重新登录
 *    弹窗（AuthErrorService + app-auth-error-modal），不再停留在空白页。
 * 3. GET /api/* 遇到 0 / 502 / 503 / 504（API 重启窗口期、nginx 尚未连上
 *    后端）→ 延迟约 1.2s 自动重放一次，覆盖"刚重启完点开页面"的时间窗。
 *
 * 安全说明：只重放幂等的 GET；401 重试最多一次且打标防循环；token 端点自身
 * 的请求直接透传（否则刷新请求失败会递归触发刷新）。
 */

/** 该请求已经是 401 刷新后的重放，不再二次刷新（防循环）。 */
const AUTH_RETRIED = new HttpContextToken<boolean>(() => false);
/** 该请求已经是瞬态故障后的重放，不再二次延迟重试。 */
const TRANSIENT_RETRIED = new HttpContextToken<boolean>(() => false);

/** 并发 401 共用同一个刷新 Promise，避免一次刷出 N 个 /connect/token 请求。 */
let refreshInFlight: Promise<unknown> | null = null;

function isTokenEndpoint(url: string): boolean {
  return url.includes('/connect/token');
}

function isApiCall(url: string): boolean {
  return url.includes('/api/');
}

function hasRefreshToken(storage: OAuthStorage): boolean {
  try {
    return !!storage.getItem('refresh_token');
  } catch {
    return false;
  }
}

/**
 * 是否存在任何身份凭证。
 *
 * <para>用于区分「登录态过期」与「游客访问受限接口」：
 * 游客（从未登录，storage 里没有任何 token）收到 401 属于正常情况，
 * 不应弹「登录已过期」——需求「未登录的情况下会显示登录已过期」。</para>
 */
function hasAnyToken(storage: OAuthStorage): boolean {
  try {
    return !!(
      storage.getItem('access_token') ||
      storage.getItem('id_token') ||
      storage.getItem('refresh_token')
    );
  } catch {
    return false;
  }
}

function clearStaleTokens(storage: OAuthStorage): void {
  for (const key of STALE_OAUTH_TOKEN_KEYS) {
    try {
      storage.removeItem(key);
    } catch {
      // 隐私模式等场景写入 storage 可能抛错，不能阻断错误处理。
    }
  }
}

function refreshOnce(oAuthService: OAuthService): Promise<unknown> {
  if (!refreshInFlight) {
    refreshInFlight = oAuthService.refreshToken().finally(() => {
      refreshInFlight = null;
    });
  }
  return refreshInFlight;
}

function withFreshToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  if (!token) return req;
  return req.clone({
    setHeaders: { Authorization: `Bearer ${token}` },
  });
}

export const authRecoveryInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> => {
  // 注意：必须在顶层同步 inject——catchError/timer 等异步回调里已失去注入上下文。
  const oAuthService = inject(OAuthService);
  const storage = inject(OAuthStorage);
  const authErrorService = inject(AuthErrorService);

  // token 端点自身失败（如 refresh_token 也过期了）必须透传，否则递归刷新。
  if (isTokenEndpoint(req.url)) {
    return next(req);
  }

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      // ── 1. 401：先尝试静默刷新 ──────────────────────────────
      if (error.status === 401 && !req.context.get(AUTH_RETRIED)) {
        // 游客（从未登录）访问需要登录的接口返回 401 是预期行为：
        // 不弹「登录已过期」，否则未登录访客一进微专业/资源页就被弹窗糊脸
        // （需求：未登录的情况下会显示登录已过期）。
        if (!hasAnyToken(storage)) {
          return throwError(() => error);
        }

        if (!hasRefreshToken(storage)) {
          // 没有续期手段：清残留、弹重新登录，不让页面停在空白/404。
          clearStaleTokens(storage);
          authErrorService.setAuthError(401, '您的登录已过期，请重新登录。');
          return throwError(() => error);
        }

        return from(refreshOnce(oAuthService)).pipe(
          switchMap(() => {
            let fresh: string | null = null;
            try {
              fresh = oAuthService.getAccessToken();
            } catch {
              fresh = null;
            }
            const replay = withFreshToken(
              req.clone({ context: req.context.set(AUTH_RETRIED, true) }),
              fresh,
            );
            return next(replay);
          }),
          catchError(() => {
            // 刷新失败（服务端重启后旧 refresh_token 已无法解密等）：
            // 清残留、弹重新登录。抛原始 401，保持页面错误语义。
            clearStaleTokens(storage);
            authErrorService.setAuthError(401, '您的登录已过期，请重新登录。');
            return throwError(() => error);
          }),
        );
      }

      // ── 2. 重启窗口期：GET /api/* 瞬态故障延迟重放一次 ─────────
      if (
        req.method === 'GET' &&
        isApiCall(req.url) &&
        !req.context.get(TRANSIENT_RETRIED) &&
        (error.status === 0 || error.status === 502 || error.status === 503 || error.status === 504)
      ) {
        const replay = req.clone({ context: req.context.set(TRANSIENT_RETRIED, true) });
        return timer(1200).pipe(switchMap(() => next(replay)));
      }

      return throwError(() => error);
    }),
  );
};
