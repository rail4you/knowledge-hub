import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';

/**
 * 仅允许 host 全局管理员（tenantId == null）通过。
 *
 * 用于 host 专属页面（如 /admin/tenant-info 租户信息管理，后端要求宿主上下文）：
 * 即使某租户角色因历史脏数据持有对应权限，也会被拦回首页，
 * 避免"菜单可见、点进去没权限"的死胡同。
 *
 * 设计（fail-closed）：
 * - currentUser 缺失或 tenantId 非空一律拦回首页；
 * - 未登录的情况由前置的 `authGuard` 处理，本守卫只关心租户上下文。
 */
export const hostOnlyGuard: CanActivateFn = () => {
  const configState = inject(ConfigStateService);
  const router = inject(Router);

  const currentUser = configState.getDeep('currentUser') as Record<string, unknown> | undefined;
  if (currentUser && (currentUser['tenantId'] as string | null | undefined) == null) {
    return true;
  }
  return router.createUrlTree(['/']);
};

/**
 * 与 `hostOnlyGuard` 等价，但用于 `canMatch`，能让路由器跳过此路由继续匹配下一条。
 */
export const hostOnlyMatchGuard: CanMatchFn = () => {
  const configState = inject(ConfigStateService);
  const currentUser = configState.getDeep('currentUser') as Record<string, unknown> | undefined;
  return !!currentUser && (currentUser['tenantId'] as string | null | undefined) == null;
};
