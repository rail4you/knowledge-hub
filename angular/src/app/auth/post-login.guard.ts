import { inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { OAuthStorage } from 'angular-oauth2-oidc';
import { hasAnyRole } from './current-user.utils';
import { ADMIN_ROLES } from './admin-roles';
import { STALE_OAUTH_TOKEN_KEYS } from '../core/auth/custom-auth.service';

/**
 * 登录后统一落点（`/post-login` 专用）。
 *
 * 背景：密码授权登录成功后，ABP 的 pipeToLogin 只会按传入的 redirectUrl 导航，
 * 而登录前拿不到用户角色，无法直接算出"该去哪"。之前默认 redirectUrl 是 `/`，
 * 导致学生登录后停在公开首页、管理端偶发停留首页（配置未就绪时守卫误放行），
 * 用户必须再点一次登录才能进自己的页面。
 *
 * 本守卫在登录成功（应用配置已刷新、角色声明就绪）之后、任何页面渲染之前执行：
 * - 未登录（直接访问本地址）：回登录页；
 * - token 租户与所选机构不一致：本地登出并打回登录页明示（见 checkTenantMatch）；
 * - 持有任一管理端角色：优先回登录前的 returnUrl（安全校验后），否则去工作台 `/admin/workbench`；
 * - 其余（学生 / 无角色）：去学生门户 `/student`（其内部默认重定向到课程中心），
 *   绝不跳管理端 returnUrl，避免学生被权限守卫拒成 403 而卡住。
 *
 * 注意与 `portalHomeGuard` 的分工：`/` 仍允许学生主动访问（门户浏览页），
 * 只有"登录刚成功"这一跳走这里做角色分发。
 */
export function resolvePostLoginUrl(
  authService: AuthService,
  configState: ConfigStateService,
  returnUrl?: string | null,
): string | null {
  if (!authService.isAuthenticated) {
    return null;
  }
  if (!hasAnyRole(configState, ADMIN_ROLES)) {
    // 学生 / 无管理角色：一律回学生门户，忽略任何 returnUrl（尤其是管理端路径），
    // 否则会被权限守卫拒成 403 而卡在错误页。
    return '/student';
  }
  // 管理端角色：优先回到登录前想去的页面（安全校验后），否则回工作台。
  if (returnUrl && isSafeInternalUrl(returnUrl)) {
    return returnUrl;
  }
  return '/admin/workbench';
}

/** returnUrl 必须是站内相对路径，防开放重定向 / 死循环。 */
function isSafeInternalUrl(url: string): boolean {
  if (!url || url[0] !== '/') return false;
  if (url.startsWith('//')) return false;
  const lower = url.toLowerCase();
  if (lower.startsWith('/account/login')) return false; // 避免回到登录页死循环
  if (lower.startsWith('/admin-login')) return false; // 宿主专用通道不自动进入
  return true;
}

/** 从 JWT 里解出 tenantid（宿主 token 无该声明返回 null；解析失败也返回 null）。 */
function decodeTokenTenantId(token: string | null | undefined): string | null {
  try {
    if (!token) return null;
    const payload = token.split('.')[1];
    if (!payload) return null;
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const json = JSON.parse(new TextDecoder().decode(bytes));
    const t = json?.tenantid;
    return typeof t === 'string' && t ? t : null;
  } catch {
    return null;
  }
}

function readTenantCookie(doc: Document): string | null {
  try {
    const m = doc.cookie.match(/(?:^|; )__tenant=([^;]*)/);
    return m ? decodeURIComponent(m[1]) : null;
  } catch {
    return null;
  }
}

export const postLoginGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const configState = inject(ConfigStateService);
  const router = inject(Router);
  const doc = inject(DOCUMENT);
  const oAuthStorage = inject(OAuthStorage);

  const returnUrl = route.queryParamMap.get('returnUrl');
  const target = resolvePostLoginUrl(authService, configState, returnUrl);
  if (!target) {
    return router.createUrlTree(['/account/login']);
  }

  // 跨租户兜底：token 的真实租户必须与登录页所选机构（`__tenant` cookie，
  // 登录页每次切换 Tab 都会同步写）一致。不一致说明本次登录串了租户
  // （如陈旧会话、并发切换等极端情况），本地登出并打回登录页明示，
  // 绝不允许静默进入错误的租户。无明确所选机构（宿主通道）时不校验。
  const intended = readTenantCookie(doc);
  const actual = decodeTokenTenantId(authService.getAccessToken());
  if (intended && actual !== intended) {
    for (const key of STALE_OAUTH_TOKEN_KEYS) {
      try {
        oAuthStorage.removeItem(key);
      } catch {
        // 隐私模式等场景写入 storage 可能抛错，不能阻断登出。
      }
    }
    try {
      doc.cookie = '__tenant=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
      doc.cookie = '__host_login=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
    } catch {
      /* ignore */
    }
    // 刷新应用状态回到匿名（fire-and-forget），再回登录页带错因由展示。
    // 注意不用 authService.logout()：它内部会 navigateByUrl('/')，与本次守卫
    // 的 UrlTree 重定向竞态，会把错误参数吞掉。
    configState.refreshAppState().subscribe();
    return router.createUrlTree(['/account/login'], {
      queryParams: { error: 'tenant-mismatch' },
    });
  }

  return router.createUrlTree([target]);
};
