import { HttpErrorResponse } from '@angular/common/http';
import { AuthErrorService } from './auth-error.service';

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
