import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasRole } from '../../auth/current-user.utils';
import { AuthErrorModalComponent } from '../../core/auth/auth-error-modal.component';

@Component({
  selector: 'app-student-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzDropDownModule,
    AuthErrorModalComponent,
  ],
  templateUrl: './student-layout.component.html',
  styleUrls: ['./student-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentLayoutComponent implements OnInit {
  private authService = inject(AuthService);
  private router = inject(Router);
  private configState = inject(ConfigStateService);

  userName = signal('用户');
  userRoleLabel = signal('学生');

  ngOnInit() {
    const currentUser = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    const userName = currentUser?.['userName'];

    if (typeof userName === 'string' && userName.trim()) {
      this.userName.set(userName);
    } else {
      const storedUser = localStorage.getItem('abp_session_state');
      if (storedUser) {
        try {
          const session = JSON.parse(storedUser);
          if (session?.username) {
            this.userName.set(session.username);
          }
        } catch {
          // ignore parse errors
        }
      }
    }

    if (hasRole(this.configState, 'Teacher')) {
      this.userRoleLabel.set('教师');
    }
  }

  get userInitial(): string {
    return this.userName()?.charAt(0)?.toUpperCase() || 'U';
  }

  /** 头像渐变背景色（基于用户名稳定生成） */
  avatarGradient(): string {
    const name = this.userName() || 'U';
    const palettes = [
      '#1e6ce8',
      '#0891b2',
      '#059669',
      '#10b981',
      '#0284c7',
      '#0c4cb8',
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = (hash * 31 + name.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
  }

  logout() {
    // ABP authService.logout() 会清除本地 token 并重定向到 IdP 的 end_session_endpoint
    // IdP 清除 session cookie 后会自动重定向回 postLogoutRedirectUri
    // 不要在 subscribe 回调中手动 window.location.href，这会覆盖 OAuth end_session 重定向
    this.authService.logout().subscribe();
  }

  /** 「就业服务」tab 是否应处于激活态 */
  isEmploymentActive(): boolean {
    const url = this.router.url || '';
    return url.startsWith('/student/employment') || url.startsWith('/student/recruitment-live');
  }

  /** 「AI 功能」tab 是否应处于激活态（AI 助手 / 智能搜索） */
  isAIActive(): boolean {
    const url = this.router.url || '';
    return url.startsWith('/student/ai') || url.startsWith('/student/search');
  }
}
