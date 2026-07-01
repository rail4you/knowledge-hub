import { Component, ChangeDetectionStrategy, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasRole } from '../auth/current-user.utils';
import { OAuthService } from 'angular-oauth2-oidc';
import { PortalService } from '../proxy/portal/portal.service';
import type { PublicHomeStatsDto, PortalHomeDataDto, TenantResourceSummaryDto, PublicBrowseDto, PublicCourseDto, PublicResourceDto, PublicMicroMajorDto, PublicBrowseFilterOption, MaterialBriefDto } from '../proxy/portal/models';
import { FilePreviewComponent } from '../shared/preview/file-preview.component';

interface HeroSlide {
  eyebrow: string;
  title: string;
  highlight: string;
  desc: string;
  primaryCta: { label: string; link: string };
  secondaryCta: { label: string; link: string };
  accent: string;
}

@Component({
  selector: 'app-portal-home',
  standalone: true,
  imports: [CommonModule, DecimalPipe, FormsModule, RouterLink, NzIconModule, NzSelectModule, NzInputModule, FilePreviewComponent],
  templateUrl: './portal-home.component.html',
  styleUrls: ['./portal-home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalHomeComponent implements OnInit, OnDestroy {
  private authService = inject(AuthService);
  private config = inject(ConfigStateService);
  private router = inject(Router);
  private portal = inject(PortalService);
  private oauthService = inject(OAuthService);

  readonly stats = signal<PublicHomeStatsDto | null>(null);
  readonly homeData = signal<PortalHomeDataDto | null>(null);
  readonly tenants = signal<TenantResourceSummaryDto[]>([]);
  readonly browseData = signal<PublicBrowseDto | null>(null);
  readonly userName = signal('');

  // Browse filters
  readonly activeTab = signal<'courses' | 'resources' | 'microMajors'>('courses');
  readonly filterTenantId = signal<string | null>(null);
  readonly filterMajorId = signal<string | null>(null);
  readonly filterSearch = signal('');

  readonly heroIndex = signal(0);
  private heroTimer: ReturnType<typeof setInterval> | null = null;

  readonly heroSlides: HeroSlide[] = [
    {
      eyebrow: '海量资源 · 一站尽览',
      title: '智慧资源库',
      highlight: '让学习更高效',
      desc: '汇聚精品课程、教案素材、实训案例，为院校提供完整的教学资源解决方案。',
      primaryCta: { label: '浏览资源库', link: '/student/resources' },
      secondaryCta: { label: '了解更多', link: '/student/courses' },
      accent: '#1a5fe0',
    },
    {
      eyebrow: '精品课程 · 名师领航',
      title: '在线课程中心',
      highlight: '名师就在身边',
      desc: '覆盖各专业核心课程，名师团队精心打造，支持在线学习、互动答疑与学习追踪。',
      primaryCta: { label: '探索课程', link: '/student/courses' },
      secondaryCta: { label: '我的学习', link: '/student/my-learning' },
      accent: '#0ea5e9',
    },
    {
      eyebrow: 'AI 驱动 · 因材施教',
      title: '智能教学助手',
      highlight: 'AI 让教学更轻松',
      desc: '智能备课、个性化学习路径、AI 答疑，为师生提供全方位的智能教学服务。',
      primaryCta: { label: '体验 AI 助手', link: '/ai/chat' },
      secondaryCta: { label: '智能搜索', link: '/search' },
      accent: '#0891b2',
    },
  ];

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  readonly rankedMaterials = () => {
    const mats = this.homeData()?.latestMaterials || [];
    return [...mats].sort((a, b) => (b.downloadCount || 0) - (a.downloadCount || 0)).slice(0, 8);
  };

  previewMaterial(m: MaterialBriefDto | PublicResourceDto): void {
    if (!m.id) return;
    // 没有文件时直接跳过（无实际文件上传的资源无法预览）
    if (!m.fileExtension && !m.originalFileName && !m.fileSize) {
      return;
    }
    let ext = m.fileExtension || '';
    if (!ext) {
      const fileName = m.originalFileName || m.name || '';
      const dot = fileName.lastIndexOf('.');
      if (dot >= 0) ext = fileName.substring(dot);
    }
    this.filePreview.open(
      m.id,
      m.originalFileName || m.name || '',
      ext,
      m.fileSize || 0
    );
  }

  canPreview(m: MaterialBriefDto | PublicResourceDto): boolean {
    return !!(m.id && (m.fileExtension || m.originalFileName && m.originalFileName.includes('.')));
  }

  readonly browseCourses = () => this.browseData()?.courses || [];
  readonly browseResources = () => this.browseData()?.resources || [];
  readonly browseMicroMajors = () => this.browseData()?.microMajors || [];
  readonly browseTenants = () => this.browseData()?.tenants || [];
  readonly browseMajors = () => this.browseData()?.majors || [];

  get isLoggedIn() { return this.authService.isAuthenticated; }
  get isStudent() {
    // 优先从 ABP ConfigState 获取
    if (hasRole(this.config, 'Student')) return true;
    // 兜底：从 JWT token claims 解析角色
    try {
      const token = this.oauthService.getAccessToken();
      if (token) {
        const payload = JSON.parse(atob(token.split('.')[1]));
        const roles = payload.role || payload.roles || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || [];
        const roleList = Array.isArray(roles) ? roles : [roles];
        return roleList.some((r: string) => r === 'Student');
      }
    } catch { }
    return false;
  }

  ngOnInit() {
    const cu = this.config.getDeep('currentUser') as Record<string, unknown> | undefined;
    if (typeof cu?.['userName'] === 'string') this.userName.set(cu['userName'] as string);

    this.portal.getPublicHomeStats().subscribe(d => this.stats.set(d));
    this.portal.getPublicTenantList().subscribe(ts => {
      this.tenants.set(ts || []);
      const id = ts?.[0]?.id;
      if (id) this.portal.getHomeData(id).subscribe(d => this.homeData.set(d));
    });

    this.loadBrowseData();
    this.startHeroAutoplay();
  }

  loadBrowseData(): void {
    this.portal.getPublicBrowse({
      tenantId: this.filterTenantId() || undefined,
      majorId: this.filterMajorId() || undefined,
      search: this.filterSearch() || undefined,
      skipCount: 0,
      maxResultCount: 50,
    }).subscribe(d => this.browseData.set(d));
  }

  setTab(tab: 'courses' | 'resources' | 'microMajors'): void {
    this.activeTab.set(tab);
    this.loadBrowseData();
  }

  onFilterChange(): void {
    this.loadBrowseData();
  }

  heroPrimaryLink(s: HeroSlide): string { return this.resolveHeroLink(s.primaryCta.link); }
  heroSecondaryLink(s: HeroSlide): string { return this.resolveHeroLink(s.secondaryCta.link); }

  private resolveHeroLink(path: string): string {
    if (!this.isLoggedIn) return '/account/login';
    if (this.isStudent) {
      const studentMap: Record<string, string> = { '/ai/chat': '/student/ai/chat', '/search': '/student/search' };
      return studentMap[path] ?? path;
    }
    const map: Record<string, string> = {
      '/student/resources': '/resources', '/student/courses': '/learning/course-list',
      '/student/my-learning': '/learning/my-courses',
    };
    return map[path] ?? path;
  }

  ngOnDestroy() { this.stopHeroAutoplay(); }

  setHeroSlide(i: number) { this.heroIndex.set(i); this.restartHeroAutoplay(); }
  private startHeroAutoplay() { this.heroTimer = setInterval(() => this.heroIndex.update(i => (i + 1) % this.heroSlides.length), 5500); }
  private stopHeroAutoplay() { if (this.heroTimer) { clearInterval(this.heroTimer); this.heroTimer = null; } }
  private restartHeroAutoplay() { this.stopHeroAutoplay(); this.startHeroAutoplay(); }

  login() { this.authService.navigateToLogin(); }
  logout() { this.authService.logout().subscribe(); }
}
