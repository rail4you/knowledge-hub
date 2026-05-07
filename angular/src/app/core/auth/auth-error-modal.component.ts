import { Component, inject, ChangeDetectionStrategy, effect } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '@abp/ng.core';
import { AuthErrorService } from './auth-error.service';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';

/**
 * 全局认证错误弹窗组件
 * 监听 AuthErrorService.showReLoginModal 信号，显示重新登录弹窗
 */
@Component({
  selector: 'app-auth-error-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule],
  template: `<!-- 弹窗由逻辑控制，无需模板 -->`
})
export class AuthErrorModalComponent {
  private readonly authErrorService = inject(AuthErrorService);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private opened = false;

  constructor() {
    // 使用 effect 监听信号变化
    effect(() => {
      const shouldShow = this.authErrorService.showReLoginModal();
      const error = this.authErrorService.authError();
      
      if (shouldShow && !this.opened && error) {
        this.opened = true;
        const isAuthError = error.status === 403;
        const errorMessage = isAuthError
          ? error.message || '您未获得授权！'
          : '您的登录已过期，请重新登录。';

        const modalRef = this.modal.confirm({
          nzTitle: isAuthError ? '权限不足' : '登录已过期',
          nzContent: `<div style="text-align:center;">
            <p style="margin-bottom:16px;color:#666;">${errorMessage}</p>
            <p style="color:#999;font-size:12px;">是否切换其他账户登录？</p>
          </div>`,
          nzOkText: '重新登录',
          nzOkType: 'primary',
          nzCancelText: '留在当前页',
          nzOnOk: () => {
            this.authErrorService.closeModal();
            this.opened = false;
            // AuthService.logout() 会清除本地 token 并重定向到 IdP 的 end_session_endpoint
            // 清除 IdP session 后，IdP 会重定向回 postLogoutRedirectUri
            this.authService.logout().subscribe();
          },
          nzOnCancel: () => {
            this.authErrorService.closeModal();
            this.opened = false;
          }
        });
        
        // 订阅 afterClose
        modalRef.afterClose.subscribe(() => {
          this.opened = false;
        });
      }
    }, { allowSignalWrites: true });
  }
}
