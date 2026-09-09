import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasAnyRole } from '../auth/current-user.utils';
import { ADMIN_ROLES } from '../auth/admin-roles';

/**
 * 学生门户守卫：只允许学生与游客访问，管理端身份一律挡在门外。
 *
 * - 未登录游客：放行（学生端支持公开浏览）；
 * - 已登录且持有任一管理端角色（Teacher / SchoolAdmin / LeagueAdmin /
 *   EnterpriseUser / admin）：直接返回 UrlTree 跳到 `/resources`，
 *   即使手动输入 URL 也进不来；
 * - 其余已登录用户（Student / 无角色）：放行。
 *
 * 与 `nonStudentGuard` 互为镜像：
 * 后台路由只认 ADMIN_ROLES，学生路由只挡 ADMIN_ROLES，
 * 两边都以角色为准，不依赖易绕过的用户名名单。
 */
export const studentPortalGuard: CanActivateFn = () => {
  const configState = inject(ConfigStateService);
  const authService = inject(AuthService);
  const router = inject(Router);

  // 未登录游客：放行（公开访问）
  if (!authService.isAuthenticated) {
    return true;
  }

  // 管理端身份优先判断：即使同时持有 Student 角色也不放行
  if (hasAnyRole(configState, ADMIN_ROLES)) {
    return router.createUrlTree(['/resources']);
  }

  return true;
};
