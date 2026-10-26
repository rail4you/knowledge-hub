import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthService, ConfigStateService } from '@abp/ng.core';

import { hasRole } from '../../auth/current-user.utils';
import { BrandingService } from '../../shared/branding/branding.service';
import { handleStudentModuleClick } from '../../shared/auth/student-module-nav';

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

  /**
   * 当前页面所属的导航分区（用于高亮顶栏对应 Tab）。
   *
   * 全站顶栏统一：首页 / 课程中心 / 资源库 / 微专业 / 院校门户 / 实训 / 就业，
   * 各公开页（门户首页 / 探索页 / 资源库列表与详情 / 院校门户 / 学校简介）完全一致。
   * 取值与「查看更多」的落点对应：
   * 课程列表 → courses、资源库列表与详情 → library、微专业列表 → micro、院校门户与学校简介 → campuses。
   */
  readonly section = input<string>('');

  /** 当前高亮的 Tab：library / resources 同属「资源库」 */
  readonly activeTab = computed<string>(() => {
    switch (this.section()) {
      case 'courses':
        return 'courses';
      case 'micro':
        return 'micro';
      case 'library':
      case 'resources':
        return 'library';
      case 'campuses':
        return 'campuses';
      default:
        return '';
    }
  });

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

  /**
   * 跳转登录页。
   *
   * @param returnUrl 登录成功后要回到的地址（学生身份登录后统一回学生门户，由登录页判定）。
   */
  login(returnUrl?: string): void {
    this.authService.navigateToLogin(returnUrl ? { returnUrl } : undefined);
  }

  /**
   * 学生端模块（实训 / 就业）入口点击处理。
   *
   * 与登录页 / 注册页共用同一份实现（shared/auth/student-module-nav.ts），
   * 保证全站顶栏行为一致。
   */
  onStudentModuleClick(event: Event, returnUrl: string): void {
    handleStudentModuleClick(this.authService, event, returnUrl);
  }

  logout(): void {
    this.authService.logout().subscribe();
  }
}
