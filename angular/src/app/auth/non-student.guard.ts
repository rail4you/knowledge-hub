import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { hasAnyRole, hasRole } from './current-user.utils';

/**
 * 仅允许"非学生"角色（Teacher / SchoolAdmin / LeagueAdmin / EnterpriseUser / admin）通过。
 *
 * 设计：
 * - 兼容多角色用户：如果用户同时具备 Student + Teacher 角色，按 Teacher 处理（放行）；
 * - 命中学生 → 重定向到学生门户 `/student`，避免回到首页又被其他路由踢回来；
 * - 未登录的情况由前置的 `authGuard` 处理，本守卫只关心角色。
 */
const isStudentOnly = (configState: ConfigStateService): boolean => {
  if (!hasRole(configState, 'Student')) {
    return false;
  }
  // 多角色：只要还兼任其他业务角色，就放行
  return !hasAnyRole(configState, ['Teacher', 'SchoolAdmin', 'LeagueAdmin', 'EnterpriseUser', 'admin']);
};

export const nonStudentGuard: CanActivateFn = () => {
  const configState = inject(ConfigStateService);
  const router = inject(Router);

  if (isStudentOnly(configState)) {
    return router.createUrlTree(['/student']);
  }
  return true;
};

/**
 * 与 `nonStudentGuard` 等价，但用于 `canMatch`，能让路由器跳过此路由继续匹配下一条，
 * 避免在 hash 链接/路径冲突时仍然加载组件。
 */
export const nonStudentMatchGuard: CanMatchFn = () => {
  const configState = inject(ConfigStateService);
  return !isStudentOnly(configState);
};
