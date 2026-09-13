import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasAnyRole, hasRole } from '../auth/current-user.utils';
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
 * - 已登录学生：跳到学生门户 `/student`，登录后不回落到公开首页；
 * - 其余已登录用户（无角色）：放行。
 *
 * 管理员优先于学生判断：即使某账号同时持有 Student + 管理角色，
 * 也一律视为管理端身份。
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

  if (hasRole(configState, 'Student')) {
    return router.createUrlTree(['/student']);
  }

  return true;
};
