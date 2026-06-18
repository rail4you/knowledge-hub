import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasRole } from '../auth/current-user.utils';

/**
 * 学生门户守卫：允许以下用户访问
 * - 学生角色用户
 * - 未登录游客（公开浏览）
 * - 教师/管理员等其他已登录用户
 */
export const studentPortalGuard: CanActivateFn = () => {
  const configState = inject(ConfigStateService);
  const authService = inject(AuthService);
  const router = inject(Router);

  // 已登录学生：放行
  if (hasRole(configState, 'Student')) {
    return true;
  }

  // 未登录游客：放行（公开访问）
  if (!authService.isAuthenticated) {
    return true;
  }

  // 已登录的教师/管理员：重定向到首页
  return router.createUrlTree(['/']);
};
