import { Injectable, signal } from '@angular/core';

/**
 * 认证错误状态管理服务
 * 拦截器设置错误状态，UI 组件监听并显示重新登录弹窗
 */
@Injectable({ providedIn: 'root' })
export class AuthErrorService {
  // 认证错误信号，{ status, message }
  authError = signal<{ status: number; message: string } | null>(null);
  
  // 重新登录弹窗是否显示
  showReLoginModal = signal(false);

  /**
   * 设置认证错误，触发弹窗显示
   */
  setAuthError(status: number, message: string) {
    this.authError.set({ status, message });
    this.showReLoginModal.set(true);
  }

  /**
   * 关闭弹窗
   */
  closeModal() {
    this.showReLoginModal.set(false);
    this.authError.set(null);
  }
}
