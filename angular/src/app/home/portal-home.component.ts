import { Component, ChangeDetectionStrategy, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterModule, ActivatedRoute } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasAnyRole, hasRole } from '../auth/current-user.utils';
import { ADMIN_ROLES } from '../auth/admin-roles';
import { PortalService } from '../proxy/portal/portal.service';
import type { PublicHomeStatsDto, TenantResourceSummaryDto, PublicBrowseDto, PublicCourseDto, PublicResourceDto, PublicMicroMajorDto, PublicBrowseFilterOption, MaterialBriefDto, CourseBriefDto, MicroMajorBriefDto, NewsBriefDto } from '../proxy/portal/models';
import { FilePreviewComponent } from '../shared/preview/file-preview.component';
import { SiteBrandComponent } from '../shared/branding/site-brand.component';
import { SiteFooterComponent } from '../shared/branding/site-footer.component';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

interface HeroSlide {
  title: string;
  highlight: string;
  desc: string;
  accent: string;
}

/** 首页跨租户聚合的课程（带 TenantName 标识，便于"全景展示"） */
interface GlobalCourseItem extends CourseBriefDto {
  tenantId: string;
  tenantName: string;
}
/** 首页跨租户聚合的微专业 */
interface GlobalMicroMajorItem extends MicroMajorBriefDto {
  tenantId: string;
  tenantName: string;
}
/** 首页跨租户聚合的资源 */
interface GlobalMaterialItem extends MaterialBriefDto {
  tenantName: string;
}
/** 首页跨租户聚合的资讯 */
interface GlobalNewsItem extends NewsBriefDto {
  tenantName: string;
}

@Component({
  selector: 'app-portal-home',
  standalone: true,
  imports: [CommonModule, DecimalPipe, FormsModule, RouterModule, NzIconModule, NzPaginationModule, NzSelectModule, NzInputModule, FilePreviewComponent, SiteBrandComponent, SiteFooterComponent],
  templateUrl: './portal-home.component.html',
  styleUrls: ['./portal-home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalHomeComponent implements OnInit, OnDestroy {
  private authService = inject(AuthService);
  private config = inject(ConfigStateService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private portal = inject(PortalService);

  readonly stats = signal<PublicHomeStatsDto | null>(null);
  readonly tenants = signal<TenantResourceSummaryDto[]>([]);
  readonly browseData = signal<PublicBrowseDto | null>(null);
  readonly userName = signal('');
  /** 资源排行榜：下载量最高的资源（跨所有租户） */
  readonly topResources = signal<MaterialBriefDto[]>([]);

  // ── 首页跨租户聚合内容（全景展示） ──
  /** 精选课程：所有租户 IsRecommended=true 的已发布课程 */
  readonly globalFeaturedCourses = signal<GlobalCourseItem[]>([]);
  /** 微专业：所有租户 Status=Published 的微专业 */
  readonly globalMicroMajors = signal<GlobalMicroMajorItem[]>([]);
  /** 最新资源：跨租户取最新若干 */
  readonly globalLatestMaterials = signal<GlobalMaterialItem[]>([]);
  /** 最新资讯：跨租户取最新若干 */
  readonly globalLatestNews = signal<GlobalNewsItem[]>([]);
  /** 加载态 */
  readonly loadingHome = signal(false);

  // ── 精品课程：默认只显示两行，展开后分页浏览全部 ──
  /** 桌面端每行约 5 张卡（minmax 300px），两行 = 10 项；移动端自动换行，仍按 10 项分页 */
  readonly featuredPageSize = 10;
  readonly featuredExpanded = signal(false);
  readonly featuredPage = signal(1);
  readonly visibleFeaturedCourses = (): GlobalCourseItem[] => {
    const all = this.globalFeaturedCourses() || [];
    if (!this.featuredExpanded()) return all.slice(0, this.featuredPageSize);
    const start = (this.featuredPage() - 1) * this.featuredPageSize;
    return all.slice(start, start + this.featuredPageSize);
  };
  toggleFeaturedExpanded(): void {
    this.featuredExpanded.update(v => !v);
    this.featuredPage.set(1);
  }
  onFeaturedPageChange(page: number): void {
    this.featuredPage.set(page);
  }

  // ── 微专业：默认只显示两行，展开后分页浏览全部 ──
  readonly microPageSize = 10;
  readonly microExpanded = signal(false);
  readonly microPage = signal(1);
  readonly visibleMicroMajors = (): GlobalMicroMajorItem[] => {
    const all = this.globalMicroMajors() || [];
    if (!this.microExpanded()) return all.slice(0, this.microPageSize);
    const start = (this.microPage() - 1) * this.microPageSize;
    return all.slice(start, start + this.microPageSize);
  };
  toggleMicroExpanded(): void {
    this.microExpanded.update(v => !v);
    this.microPage.set(1);
  }
  onMicroPageChange(page: number): void {
    this.microPage.set(page);
  }

  // Browse filters
  readonly activeTab = signal<'courses' | 'resources' | 'microMajors'>('courses');
  readonly filterTenantId = signal<string | null>(null);
  readonly filterMajorId = signal<string | null>(null);
  readonly filterSearch = signal('');
  /** 专业下拉中的“公共课”哨兵值（后端无归属专业的课程） */
  readonly majorPublicOnlyValue = '__public__';

  // ── 全部资源：课程 / 资源 / 微专业各 tab 独立分页 ──
  readonly browsePageSize = 10;
  readonly browseCoursePage = signal(1);
  readonly browseResourcePage = signal(1);
  readonly browseMicroPage = signal(1);
  readonly browseCourseTotal = () => this.browseData()?.totalCourseCount ?? 0;
  readonly browseResourceTotal = () => this.browseData()?.totalResourceCount ?? 0;
  readonly browseMicroTotal = () => this.browseData()?.totalMicroMajorCount ?? 0;

  readonly heroIndex = signal(0);
  private heroTimer: ReturnType<typeof setInterval> | null = null;

  readonly heroSlides: HeroSlide[] = [
    {
      title: '智慧资源库',
      highlight: '让学习更高效',
      desc: '汇聚精品课程、教案素材、实训案例，为院校提供完整的教学资源解决方案。',
      accent: '#7cc4ff',
    },
    {
      title: '在线课程中心',
      highlight: '名师就在身边',
      desc: '覆盖各专业核心课程，名师团队精心打造，支持在线学习、互动答疑与学习追踪。',
      accent: '#7cc4ff',
    },
    {
      title: '智能教学助手',
      highlight: 'AI 让教学更轻松',
      desc: '智能备课、个性化学习路径、AI 答疑，为师生提供全方位的智能教学服务。',
      accent: '#7cc4ff',
    },
  ];

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  /** 最新资源榜：取前五项 */
  readonly latestResourcesTop5 = () => (this.globalLatestMaterials() || []).slice(0, 5);

  /** 资源排行版：取前五项 */
  readonly topResourcesTop5 = () => (this.topResources() || []).slice(0, 5);

  previewMaterial(m: MaterialBriefDto | GlobalMaterialItem | PublicResourceDto): void {
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

  canPreview(m: MaterialBriefDto | GlobalMaterialItem | PublicResourceDto): boolean {
    // 有扩展名或原文件名，或文件大小 > 0（说明确实有文件）即可预览。
    // 早期 bug 曾把 FileExtension/OriginalFileName 清空，仅靠 fileSize 也能兜底。
    return !!(m.id && (m.fileExtension || (m.originalFileName && m.originalFileName.includes('.')) || m.fileSize > 0));
  }

  readonly browseCourses = () => this.browseData()?.courses || [];
  readonly browseResources = () => this.browseData()?.resources || [];
  readonly browseMicroMajors = () => this.browseData()?.microMajors || [];
  readonly browseTenants = () => this.browseData()?.tenants || [];
  readonly browseMajors = () => this.browseData()?.majors || [];

  get isLoggedIn() { return this.authService.isAuthenticated; }
  private readonly knownStudents = new Set(['zmq', 'student', 'hoststudent', 'qidistudent', 'stu', 'std01', 'stutest', 'teststu', 'teststudent123', 'kEMlzpAX']);
  get isStudent(): boolean {
    if (hasRole(this.config, 'Student')) return true;
    const cu = this.config.getDeep('currentUser') as Record<string, unknown> | undefined;
    return this.knownStudents.has((cu?.['userName'] as string) || '');
  }
  get isTeacher(): boolean {
    return this.isLoggedIn && hasAnyRole(this.config, ADMIN_ROLES);
  }

  ngOnInit() {
    // 第二道防线（第一道是路由层的 portalHomeGuard，组件通常不会被实例化）。
    // 保留此处兜底：若守卫执行时角色声明尚未就绪，组件内再次拦截。
    if (this.isLoggedIn && this.isTeacher) {
      const returnUrl = this.route.snapshot.queryParams['returnUrl'];
      if (!returnUrl) this.router.navigate(['/resources']);
      return;
    }

    const cu = this.config.getDeep('currentUser') as Record<string, unknown> | undefined;
    if (typeof cu?.['userName'] === 'string') this.userName.set(cu['userName'] as string);

    // 从微专业详情页“返回首页”时携带 section=microMajors，回到首页的“微专业”模块位置。
    // 兼容旧链接 ?tab=microMajors：同样定位到“微专业”模块（不再去底部的“全部资源”）。
    const section = this.route.snapshot.queryParamMap.get('section');
    if (section === 'microMajors') {
      this.scrollToSection('micro-majors');
    } else {
      const browseTab = this.route.snapshot.queryParamMap.get('tab');
      if (browseTab === 'courses' || browseTab === 'resources' || browseTab === 'microMajors') {
        this.activeTab.set(browseTab);
        if (browseTab === 'microMajors') {
          this.scrollToSection('micro-majors');
        } else {
          this.scrollToBrowse();
        }
      }
    }

    this.portal.getPublicHomeStats().subscribe(d => this.stats.set(d));
    this.portal.getPublicTenantList().subscribe(ts => {
      this.tenants.set(ts || []);
      this.loadGlobalHomeData(ts || []);
    });

    // 资源排行榜：跨所有租户取下载量最高的前 5 项
    this.portal.getTopResourcesByDownload(5).subscribe(d => this.topResources.set(d || []));

    this.loadBrowseData();
    this.startHeroAutoplay();
  }

  /**
   * 首页全景展示：拉取所有有数据租户的首页数据，合并精选课程/微专业/最新资源/最新资讯。
   * 只跳过课程+资源+微专业都为 0 的空租户，避免对无人租户发起无意义的请求。
   */
  loadGlobalHomeData(tenants: TenantResourceSummaryDto[]): void {
    const activeTenants = (tenants || []).filter(
      t => (t.courseCount || 0) + (t.resourceCount || 0) + (t.microMajorCount || 0) > 0
    );
    if (activeTenants.length === 0) {
      this.loadingHome.set(false);
      return;
    }

    this.loadingHome.set(true);

    // 为每个租户构建一个 tenantName 映射（沿用 TenantResourceSummary 的展示名）
    const tenantNames = new Map<string, string>();
    for (const t of activeTenants) {
      tenantNames.set(t.id, t.tenantName || t.name);
    }

    const requests = activeTenants.map(t =>
      this.portal.getHomeData(t.id).pipe(
        map(home => ({ tenantId: t.id, tenantName: tenantNames.get(t.id) || t.name, home })),
        catchError(() => of({ tenantId: t.id, tenantName: tenantNames.get(t.id) || t.name, home: null }))
      )
    );

    forkJoin(requests).subscribe(results => {
      const courses: GlobalCourseItem[] = [];
      const microMajors: GlobalMicroMajorItem[] = [];
      const materials: GlobalMaterialItem[] = [];
      const news: GlobalNewsItem[] = [];

      for (const r of results) {
        const h = r.home as any;
        if (!h) continue;
        const tn = r.tenantName;
        const tid = r.tenantId;
        for (const c of (h.featuredCourses || [])) {
          courses.push({ ...c, tenantId: tid, tenantName: tn });
        }
        for (const m of (h.microMajors || [])) {
          microMajors.push({ ...m, tenantId: tid, tenantName: tn });
        }
        for (const mat of (h.latestMaterials || [])) {
          materials.push({ ...mat, tenantName: tn });
        }
        for (const n of (h.latestNews || [])) {
          news.push({ ...n, tenantName: tn });
        }
      }

      // 跨租户拼接：后端每个租户的 latestMaterials/latestNews 内部已按时间倒序，
      // 跨租户拼接时按 activeTenants 顺序保持稳定即可，避免依赖未在 DTO 中的 CreationTime 字段。
      news.sort((a, b) => {
        const ta = a.publishedAt ? Date.parse(a.publishedAt as any) : 0;
        const tb = b.publishedAt ? Date.parse(b.publishedAt as any) : 0;
        return tb - ta;
      });

      // 精品课程 / 微专业：保留全量，前端按“默认两行 + 展开分页”展示
      this.globalFeaturedCourses.set(courses);
      this.globalMicroMajors.set(microMajors);
      this.globalLatestMaterials.set(materials.slice(0, 8));
      this.globalLatestNews.set(news.slice(0, 5));
      this.loadingHome.set(false);
    });
  }

  loadBrowseData(): void {
    const page = this.activeTab() === 'courses'
      ? this.browseCoursePage()
      : this.activeTab() === 'resources'
        ? this.browseResourcePage()
        : this.browseMicroPage();
    const skipCount = (page - 1) * this.browsePageSize;
    const majorSel = this.filterMajorId();
    const publicOnly = majorSel === this.majorPublicOnlyValue;
    this.portal.getPublicBrowse(
      this.filterTenantId() || undefined,
      !publicOnly ? majorSel || undefined : undefined,
      this.filterSearch() || undefined,
      skipCount,
      this.browsePageSize,
      publicOnly ? true : undefined,
    ).subscribe(d => this.browseData.set(d));
  }

  /** 是否有生效中的筛选（租户 / 专业 / 搜索任一非空） */
  readonly hasActiveFilters = (): boolean =>
    !!(this.filterTenantId() || this.filterMajorId() || this.filterSearch());

  /** 一键清除全部筛选并回到第一页 */
  clearFilters(): void {
    this.filterTenantId.set(null);
    this.filterMajorId.set(null);
    this.filterSearch.set('');
    this.onFilterChange();
  }

  /** 课程卡片的专业文本：多专业用“、”连接，无归属显示“公共课” */
  courseMajorText(c: { majorNames?: string[] | null; majorName?: string | null } | null | undefined): string {
    const names = (c?.majorNames || []).filter(n => !!n);
    if (names.length > 0) return names.join('、');
    const single = (c?.majorName || '').trim();
    return single || '公共课';
  }

  setTab(tab: 'courses' | 'resources' | 'microMajors'): void {
    this.activeTab.set(tab);
    this.loadBrowseData();
  }

  onFilterChange(): void {
    // 筛选变化时各 tab 页码归 1，避免 skip 越界导致空白页
    this.browseCoursePage.set(1);
    this.browseResourcePage.set(1);
    this.browseMicroPage.set(1);
    this.loadBrowseData();
  }

  onBrowseCoursePageChange(page: number): void {
    this.browseCoursePage.set(page);
    this.loadBrowseData();
  }

  onBrowseResourcePageChange(page: number): void {
    this.browseResourcePage.set(page);
    this.loadBrowseData();
  }

  onBrowseMicroPageChange(page: number): void {
    this.browseMicroPage.set(page);
    this.loadBrowseData();
  }

  ngOnDestroy() { this.stopHeroAutoplay(); }

  setHeroSlide(i: number) { this.heroIndex.set(i); this.restartHeroAutoplay(); }
  private startHeroAutoplay() { this.heroTimer = setInterval(() => this.heroIndex.update(i => (i + 1) % this.heroSlides.length), 5500); }
  private stopHeroAutoplay() { if (this.heroTimer) { clearInterval(this.heroTimer); this.heroTimer = null; } }
  private restartHeroAutoplay() { this.stopHeroAutoplay(); this.startHeroAutoplay(); }

  /**
   * 滚动到首页指定模块（微专业详情页返回首页时定位到“微专业”模块）。
   * 微专业模块是异步加载的（@if 满足才渲染），位置可能较晚才出现，
   * 因此多次重试以确保内容加载完成后仍能定位。
   */
  private scrollToSection(elementId: string): void {
    const jump = () => document.getElementById(elementId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    for (const delay of [100, 500, 1200, 2500]) {
      window.setTimeout(jump, delay);
    }
  }

  /**
   * 滚动到“全部资源”区域（课程/资源 tab 返回时定位）。
   * 由于上方各 section 的卡片是异步加载的（高度会变化），平滑滚动执行两次以修正位置。
   */
  private scrollToBrowse(): void {
    const el = () => document.getElementById('browse');
    const jump = () => el()?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    window.setTimeout(jump, 60);
    window.setTimeout(jump, 500);
  }

  // ---- 文件扩展名对应的图标 ----
  /** 格式化文件大小（与学生端一致） */
  formatFileSize(bytes?: number): string {
    if (!bytes || bytes <= 0) return '未知大小';
    const units = ['B', 'KB', 'MB', 'GB'];
    let size = bytes;
    let i = 0;
    while (size >= 1024 && i < units.length - 1) { size /= 1024; i++; }
    return `${size.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
  }

  /** 根据文件扩展名返回对应的图标名 */  materialIcon(ext?: string): string {
    if (!ext) return 'file';
    const e = ext.toLowerCase();
    if (e.includes('mp4') || e.includes('avi') || e.includes('mov') || e.includes('flv')) return 'video-camera';
    if (e.includes('pdf')) return 'file-pdf';
    if (e.includes('ppt')) return 'file-ppt';
    if (e.includes('doc')) return 'file-word';
    if (e.includes('xls')) return 'file-excel';
    if (e.includes('zip') || e.includes('rar') || e.includes('7z')) return 'file-zip';
    if (e.includes('png') || e.includes('jpg') || e.includes('gif') || e.includes('webp')) return 'file-image';
    return 'file-text';
  }

  login() { this.authService.navigateToLogin(); }
  logout() { this.authService.logout().subscribe(); }
}
