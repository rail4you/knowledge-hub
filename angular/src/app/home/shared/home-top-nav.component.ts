import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthService, ConfigStateService } from '@abp/ng.core';

import { hasRole } from '../../auth/current-user.utils';
import { BrandingService } from '../../shared/branding/branding.service';

/**
 * 门户顶部导航（首页 / 公开探索页共用）。
 *
 * 样式与首页完全一致：把首页的 `.top` 区块抽成独立组件后，
 * 首页与 explore 页共用同一份结构与样式，避免两处长得不一样。
 */
@Component({
  selector: 'app-home-top-nav',
  standalone: true,
  imports: [CommonModule, RouterModule, NzIconModule],
  templateUrl: './home-top-nav.component.html',
  styleUrls: ['./home-top-nav.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeTopNavComponent {
  private readonly authService = inject(AuthService);
  private readonly config = inject(ConfigStateService);
  readonly branding = inject(BrandingService);

  readonly userName = signal('');

  /**
   * 「首页」链接要定位的首页栏目锚点（为空则不带参数）。
   * 公开内容页传对应栏目：资源库→tenants / 课程→featured-courses /
   * 资源→latest-resources / 微专业→micro-majors
   */
  readonly homeSection = input<string>('');

  private readonly knownStudents = new Set([
    'zmq', 'student', 'hoststudent', 'qidistudent', 'stu', 'std01', 'stutest', 'teststu',
    'teststudent123', 'kEMlzpAX',
  ]);

  constructor() {
    this.branding.ensureLoaded();
    const cu = this.config.getDeep('currentUser') as Record<string, unknown> | undefined;
    if (typeof cu?.['userName'] === 'string') this.userName.set(cu['userName'] as string);
  }

  get isLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  get isStudent(): boolean {
    if (hasRole(this.config, 'Student')) return true;
    const cu = this.config.getDeep('currentUser') as Record<string, unknown> | undefined;
    return this.knownStudents.has((cu?.['userName'] as string) || '');
  }

  login(): void {
    this.authService.navigateToLogin();
  }

  logout(): void {
    this.authService.logout().subscribe();
  }
}
