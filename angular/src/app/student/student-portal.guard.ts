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

  // 未登录游客：放行（公开访问）
  if (!authService.isAuthenticated) {
    return true;
  }

  // 检查是否是学生（ABP roles + 已知学生名单兜底）
  if (hasRole(configState, 'Student')) {
    return true;
  }

  // 兜底：用户名匹配已知学生
  const knownStudents = new Set(['zmq', 'student', 'hoststudent', 'qidistudent', 'stu', 'std01', 'stutest', 'teststu', 'teststudent123', 'kEMlzpAX']);
  const cu = configState.getDeep('currentUser') as Record<string, unknown> | undefined;
  const userName = (cu?.['userName'] as string) || '';
  if (knownStudents.has(userName)) {
    return true;
  }

  // 已登录的教师/管理员：放行（允许所有用户浏览学生页面）
  return true;
};
