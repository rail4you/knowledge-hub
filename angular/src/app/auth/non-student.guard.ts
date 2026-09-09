import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { hasAnyRole } from './current-user.utils';
import { ADMIN_ROLES } from './admin-roles';

/**
 * 仅允许管理端角色（见 ADMIN_ROLES）通过。
 *
 * 设计：
 * - 安全（fail-closed）：只有明确具备某个非学生管理角色才放行进入后台；
 * - 没有任何管理角色（包括学生、或角色声明缺失/未分配的无角色账号）一律重定向到学生门户 `/student`，
 *   避免无角色/学生账号被错误地当成管理员进入后台；
 * - 未登录的情况由前置的 `authGuard` 处理，本守卫只关心角色。
 */
export const nonStudentGuard: CanActivateFn = () => {
  const configState = inject(ConfigStateService);
  const router = inject(Router);

  if (hasAnyRole(configState, ADMIN_ROLES)) {
    return true;
  }
  return router.createUrlTree(['/student']);
};

/**
 * 与 `nonStudentGuard` 等价，但用于 `canMatch`，能让路由器跳过此路由继续匹配下一条，
 * 避免在 hash 链接/路径冲突时仍然加载组件。
 */
export const nonStudentMatchGuard: CanMatchFn = () => {
  const configState = inject(ConfigStateService);
  return hasAnyRole(configState, ADMIN_ROLES);
};
