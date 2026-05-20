import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
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
    NzAvatarModule,
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

  logout() {
    this.authService.logout().subscribe(() => {
      // 使用完整页面刷新清除 ABP 框架缓存的布局状态
      // 避免 SPA 路由跳转导致退出后页面状态异常
      window.location.href = '/';
    });
  }
}
