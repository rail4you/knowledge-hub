import { Component, inject, ViewChild, OnInit, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { RouterLink, Router, ActivatedRoute } from '@angular/router';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { hasRole } from '../auth/current-user.utils';
import { PortalService } from '../proxy/portal/portal.service';
import { FilePreviewComponent } from '../shared/preview/file-preview.component';
import type { TenantResourceSummaryDto, PublicHomeStatsDto, PortalHomeDataDto } from '../proxy/portal/models';

@Component({
  selector: 'app-home',
  standalone: true,
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  imports: [CommonModule, DecimalPipe, RouterLink, NzSpinModule, NzEmptyModule, NzIconModule, FilePreviewComponent],
})
export class HomeComponent implements OnInit {
  private authService = inject(AuthService);
  private configService = inject(ConfigStateService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private portalService = inject(PortalService);

  readonly stats = signal<PublicHomeStatsDto | null>(null);
  readonly tenants = signal<TenantResourceSummaryDto[]>([]);
  readonly homeData = signal<PortalHomeDataDto | null>(null);
  readonly loadingStats = signal(false);

  @ViewChild(FilePreviewComponent) filePreview!: FilePreviewComponent;
  readonly loadingTenants = signal(false);

  get hasLoggedIn(): boolean { return this.authService.isAuthenticated; }
  get isStudent(): boolean { return hasRole(this.configService, 'Student'); }

  ngOnInit() {
    this.loadStats();
    this.loadTenants();
    if (this.hasLoggedIn && this.isStudent) {
      const returnUrl = this.route.snapshot.queryParams['returnUrl'];
      if (!returnUrl) this.router.navigate(['/student']);
    }
  }

  loadStats() {
    this.loadingStats.set(true);
    this.portalService.getPublicHomeStats().subscribe({
      next: data => { this.stats.set(data); this.loadingStats.set(false); },
      error: () => this.loadingStats.set(false),
    });
  }

  loadTenants() {
    this.loadingTenants.set(true);
    this.portalService.getPublicTenantList().subscribe({
      next: tenants => {
        this.tenants.set(tenants || []);
        this.loadingTenants.set(false);
        // Load home data for the first tenant (or default)
        const firstId = tenants?.[0]?.id;
        if (firstId) this.loadHomeData(firstId);
      },
      error: () => this.loadingTenants.set(false),
    });
  }

  loadHomeData(tenantId: string) {
    this.portalService.getHomeData(tenantId).subscribe({
      next: data => this.homeData.set(data),
    });
  }

  login() { this.authService.navigateToLogin(); }
  logout() {
    // ABP authService.logout() 会清除本地 token 并重定向到 IdP 的 end_session_endpoint
    // IdP 清除 session cookie 后会自动重定向回 postLogoutRedirectUri
    // 不要在 subscribe 回调中手动 window.location.href，这会覆盖 OAuth end_session 重定向
    this.authService.logout().subscribe();
  }

  previewResource(resource: { id?: string; name?: string; fileExtension?: string | null; downloadCount?: number }): void {
    if (!resource.id) return;
    this.filePreview?.open(resource.id, resource.name || '', resource.fileExtension || '', 0);
  }

  coverGradient(index: number): string {
    const p = ['#1e6ce8,#00b7ff', '#0c4cb8,#3b82f6', '#2563eb,#1e6ce8', '#0284c7,#0ea5e9', '#1d4ed8,#6366f1'];
    return `linear-gradient(135deg, ${p[index % p.length]})`;
  }
}
