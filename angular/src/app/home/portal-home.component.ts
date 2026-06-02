import { Component, ChangeDetectionStrategy, inject, OnInit, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasRole } from '../auth/current-user.utils';
import { PortalService } from '../proxy/portal/portal.service';
import type { PublicHomeStatsDto, PortalHomeDataDto } from '../proxy/portal/models';

@Component({
  selector: 'app-portal-home',
  standalone: true,
  imports: [CommonModule, DecimalPipe, RouterLink, NzIconModule],
  templateUrl: './portal-home.component.html',
  styleUrls: ['./portal-home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalHomeComponent implements OnInit {
  private authService = inject(AuthService);
  private config = inject(ConfigStateService);
  private router = inject(Router);
  private portal = inject(PortalService);

  readonly stats = signal<PublicHomeStatsDto | null>(null);
  readonly homeData = signal<PortalHomeDataDto | null>(null);
  readonly userName = signal('');

  get isLoggedIn() { return this.authService.isAuthenticated; }
  get isStudent() { return hasRole(this.config, 'Student'); }

  ngOnInit() {
    const cu = this.config.getDeep('currentUser') as Record<string,unknown> | undefined;
    if (typeof cu?.['userName'] === 'string') this.userName.set(cu['userName'] as string);

    // 登录后按角色跳转：教师/管理员 → 管理后台，学生 → 留在首页
    if (this.isLoggedIn && !this.isStudent) {
      this.router.navigate(['/resources']);
      return;
    }

    this.portal.getPublicHomeStats().subscribe(d => this.stats.set(d));
    this.portal.getPublicTenantList().subscribe(ts => {
      const id = ts?.[0]?.id;
      if (id) this.portal.getHomeData(id).subscribe(d => this.homeData.set(d));
    });
  }

  login() { this.authService.navigateToLogin(); }

  logout() {
    // ABP authService.logout() 会清除本地 token 并重定向到 IdP 的 end_session_endpoint
    // IdP 清除 session cookie 后会自动重定向回 postLogoutRedirectUri
    this.authService.logout().subscribe();
  }
}
