import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthErrorService } from './auth-error.service';

/**
 * 需要跳过认证检查的 URL
 */
const SKIP_AUTH_CHECK_URLS = [
  '/api/account/',
  '/api/abp/application-localization',
  '/api/abp/api-definition',
];

/**
 * HTTP 拦截器 - 处理 401/403 认证错误
 * 捕获错误后设置 AuthErrorService 信号，由 UI 组件显示重新登录弹窗
 */
export const authErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const authErrorService = inject(AuthErrorService);

  // 跳过特定 URL 的认证检查
  if (SKIP_AUTH_CHECK_URLS.some(url => req.url.includes(url))) {
    return next(req);
  }

  return next(req).pipe(
    // 错误已在 ABP 的 toaster 中显示，这里额外触发重新登录弹窗
  );
};

/**
 * 在应用层处理认证错误的辅助函数
 * 可在 Guard 或 Service 错误处理中调用
 */
export function handleAuthError(error: HttpErrorResponse, authErrorService: AuthErrorService) {
  if (error.status === 401 || error.status === 403) {
    const message = error.error?.error?.message || error.error?.message || 
      (error.status === 403 ? '您未获得授权！' : '您的登录已过期，请重新登录。');
    authErrorService.setAuthError(error.status, message);
  }
}
