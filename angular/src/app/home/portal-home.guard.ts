import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasAnyRole } from '../auth/current-user.utils';
import { ADMIN_ROLES } from '../auth/admin-roles';

/**
 * 门户首页守卫（`/` 与 `/tenant/:id` 共用）。
 *
 * 根本性修复：之前管理员登录后先渲染 `PortalHomeComponent`，
 * 再靠组件 `ngOnInit` 里 `router.navigate` 做延迟跳转，
 * 中间存在一个时间窗口——管理员能看到首页并点进 `/student/**` 页面。
 *
 * 本守卫在路由层面（组件实例化之前）即时拦截：
 * - 未登录游客：放行（首页/租户主页本身是公开浏览页）；
 * - 已登录且持有任一管理端角色：直接返回 UrlTree 跳到系统工作台 `/admin/workbench`，
 *   组件永远不会被创建，不存在"先看到首页再跳走"的窗口；
 * - 已登录学生：放行。学生端头部「主站」按钮（`routerLink="/"`）需要能回到
 *   门户首页浏览，之前在此拦截跳回 `/student` 会导致按钮看起来"点不动"。
 *   首页组件本身已支持登录态（显示「学生门户」入口），学生可自行往返两端。
 * - 其余已登录用户（无角色）：放行。
 *
 * 只要持有任一管理端角色（即使同时持有 Student）就一律视为管理端身份。
 */
export const portalHomeGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const configState = inject(ConfigStateService);
  const router = inject(Router);

  if (!authService.isAuthenticated) {
    return true;
  }

  if (hasAnyRole(configState, ADMIN_ROLES)) {
    return router.createUrlTree(['/admin/workbench']);
  }

  return true;
};
