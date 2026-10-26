import { AuthService } from '@abp/ng.core';

/**
 * 学生端模块（实训 / 就业）入口的**统一处理逻辑**。
 *
 * <p>全站顶栏（门户首页、公开内容页、登录页、注册页）共用同一套行为：
 * - 已登录：放行 <c>routerLink</c>，直接进入对应模块；
 * - 未登录：拦截跳转，先跳登录页，并带上 <c>returnUrl</c>，
 *   登录成功后自动回到该模块。</p>
 *
 * @param authService ABP 认证服务（判断登录态 + 跳转登录页）。
 * @param event 链接的点击事件（未登录时需要 preventDefault 阻止路由跳转）。
 * @param returnUrl 登录后要回到的模块地址。
 */
export function handleStudentModuleClick(
  authService: AuthService,
  event: Event,
  returnUrl: string,
): void {
  if (authService.isAuthenticated) {
    // 已登录：交给 routerLink 正常跳转
    return;
  }

  event.preventDefault();
  authService.navigateToLogin({ returnUrl });
}
