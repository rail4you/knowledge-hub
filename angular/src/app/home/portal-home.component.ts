import { Component, ChangeDetectionStrategy, computed, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
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
import { BrandingService } from '../shared/branding/branding.service';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

/** 首页轮播幻灯片（严格对齐原型 home-v2） */
interface HeroSlide {
  /** 背景图（本地资产） */
  img: string;
  /** 底部 selector 标签，如 '01 / 开启学习' */
  label: string;
  /** 主标题（含 <br>） */
  title: string;
  /** 描述（含 <br>） */
  desc: string;
  /** CTA 文案 */
  button: string;
  /** CTA 跳转路由 */
  to: string;
  /** CTA 下方小字 */
  small: string;
}

/** 资源库卡片（由租户 + 该租户的精选课程合成） */
interface LibraryCardVm {
  id: string;
  name: string;
  desc: string;
  tone: 'sage' | 'blue' | 'sand';
  icon: string;
  num: string;
  courseCount: number;
  resourceCount: number;
  microMajorCount: number;
  /** 该资源库下展示的课程名（最多 3 条） */
  courses: { id: string; title: string }[];
}

/** 微专业卡片（对齐原型 microcard 结构） */
interface MicroCardVm {
  id: string;
  name: string;
  category: string;
  outcome: string;
  desc: string;
  count: number;
  duration: string;
  org: string;
  tone: 'sage' | 'blue' | 'sand';
  /** 「你将学习」列表（用同租户课程兜底） */
  lessons: { id: string; title: string }[];
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
  imports: [CommonModule, FormsModule, RouterModule, NzIconModule, NzPaginationModule, NzSelectModule, NzInputModule, FilePreviewComponent],
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
  readonly branding = inject(BrandingService);

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
  /** 加载态：首屏数据（租户列表 + 全站首页数据）未就绪时为 true，用于骨架占位避免跳动 */
  readonly loadingHome = signal(true);

  // ── 首页卡片视图模型（home-v2 结构） ──
  private readonly TONES: Array<'sage' | 'blue' | 'sand'> = ['sage', 'blue', 'sand'];
  private readonly TONE_ICONS: string[] = ['read', 'thunderbolt', 'appstore'];

  /** 资源库卡片：由租户列表 + 该租户下的精选课程合成 */
  readonly libraryCards = computed<LibraryCardVm[]>(() => {
    const tenants = this.tenants() || [];
    const courses = this.globalFeaturedCourses() || [];
    return tenants.map((t, i) => {
      const own = courses.filter(c => c.tenantId === t.id);
      const tone = this.TONES[i % this.TONES.length];
      return {
        id: t.id,
        name: t.tenantName || t.name || '资源库',
        desc: t.description || t.tenantDescription || '汇聚专业课程与配套资源，先找到方向，再开始学习。',
        tone,
        icon: this.TONE_ICONS[i % this.TONE_ICONS.length],
        num: String(i + 1).padStart(2, '0'),
        courseCount: t.courseCount || 0,
        resourceCount: t.resourceCount || 0,
        microMajorCount: t.microMajorCount || 0,
        courses: own.slice(0, 3).map(c => ({ id: c.id as string, title: c.title || '' })),
      };
    });
  });

  /** 微专业卡片（对齐原型 microcard 结构） */
  readonly microCards = computed<MicroCardVm[]>(() => {
    const majors = this.globalMicroMajors() || [];
    const courses = this.globalFeaturedCourses() || [];
    return majors.map((m, i) => {
      // 原型「你将学习」列表用同租户课程兜底（后端 DTO 未暴露微专业-课程明细）
      const lessons = courses
        .filter(c => c.tenantId === m.tenantId)
        .slice(0, 3)
        .map(c => ({ id: c.id as string, title: c.title || '' }));
      return {
        id: m.id as string,
        name: m.title || '微专业',
        category: m.tenantName || '专业方向',
        outcome: '从专业理解，走向系统能力',
        desc: '系统化课程体系，跨学科融合培养，由资深团队打造，助力专业能力全面进阶。',
        count: m.courseCount || 0,
        duration: '建议 6-8 周',
        org: m.tenantName || '高校',
        tone: this.TONES[i % this.TONES.length],
        lessons,
      };
    });
  });

  /** 首页精选课程（取前 3，用于 featured-section） */
  readonly homeFeaturedCourses = computed<GlobalCourseItem[]>(() =>
    (this.globalFeaturedCourses() || []).slice(0, 3)
  );

  /** 首页微专业（取前 3，用于 micro-section） */
  readonly homeMicroMajors = computed<MicroCardVm[]>(() =>
    (this.microCards() || []).slice(0, 3)
  );

  // ── 微专业方向筛选（对齐原型 micro-tabs） ──
  readonly microFilter = signal('全部方向');
  readonly microFilters = computed<string[]>(() => {
    const cats = new Set<string>();
    (this.microCards() || []).forEach(m => { if (m.category) cats.add(m.category); });
    return ['全部方向', ...cats];
  });
  setMicroFilter(f: string): void { this.microFilter.set(f); }
  readonly visibleMicroCards = computed<MicroCardVm[]>(() => {
    const f = this.microFilter();
    const all = this.microCards() || [];
    const filtered = f === '全部方向' ? all : all.filter(m => m.category === f);
    return filtered.slice(0, 3);
  });

  /** 首页最新资源（取前 3） */
  readonly homeMaterials = computed<GlobalMaterialItem[]>(() =>
    (this.globalLatestMaterials() || []).slice(0, 3)
  );

  /** 首页最新资讯（取前 3） */
  readonly homeNews = computed<GlobalNewsItem[]>(() =>
    (this.globalLatestNews() || []).slice(0, 3)
  );

  /** 首页课程封面色（无封面图时的占位色，对齐原型 c0~c3） */
  coverTone(index: number): string {
    return ['c0', 'c1', 'c2', 'c3'][index % 4];
  }

  /** 课程封面大字（标题过长时截断） */
  coverTitle(title?: string | null): string {
    const t = (title || '').trim();
    return t.length > 12 ? t.slice(0, 12) + '…' : t;
  }

  /** 文件类型展示（资源卡片左侧文件符） */
  fileExtLabel(ext?: string | null): string {
    return (ext || 'FILE').toUpperCase().replace(/^\./, '');
  }

  /** 是否为视频类资源 */
  isVideo(ext?: string | null): boolean {
    const e = (ext || '').toLowerCase();
    return e.includes('mp4') || e.includes('avi') || e.includes('mov') || e.includes('flv') || e.includes('mkv');
  }

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
  // 首页「全部资源」板块三个 tab 共享的页大小。
  // 课程 tab 首批数据多，按 6 个分页以避免首屏过于冗长；
  // 资源 / 微专业 tab 保留同样节奏以统一观感。
  readonly browsePageSize = 6;
  readonly browseCoursePage = signal(1);
  readonly browseResourcePage = signal(1);
  readonly browseMicroPage = signal(1);
  readonly browseCourseTotal = () => this.browseData()?.totalCourseCount ?? 0;
  readonly browseResourceTotal = () => this.browseData()?.totalResourceCount ?? 0;
  readonly browseMicroTotal = () => this.browseData()?.totalMicroMajorCount ?? 0;

  readonly heroIndex = signal(0);
  readonly heroPaused = signal(false);
  private heroTimer: ReturnType<typeof setInterval> | null = null;

  /** 首页轮播（严格对齐原型 home-v2，背景图取自原型资产） */
  readonly heroSlides: HeroSlide[] = [
    {
      img: 'assets/images/home-v2/hero-1.webp',
      label: '01 / 开启学习',
      title: '让每一份知识，<br>连接更辽阔的未来。',
      desc: '汇聚优质课程与专业资源，<br>让每一次探索，都成为成长的起点。',
      button: '探索精品课程',
      to: '/student/courses',
      small: '课程 · 资源 · 实践 · 成长',
    },
    {
      img: 'assets/images/home-v2/hero-2.webp',
      label: '02 / 发现微专业',
      title: '以一份专注，<br>打开新的专业可能。',
      desc: '沿着清晰的课程路径，<br>从兴趣出发，走向系统的专业能力。',
      button: '发现特色微专业',
      to: '/student/micro-majors',
      small: '系统课程 / 分阶段学习 / 实践应用',
    },
    {
      img: 'assets/images/home-v2/hero-3.webp',
      label: '03 / 走向实践',
      title: '把课堂所学，<br>变成解决问题的能力。',
      desc: '连接真实任务与专业实践，<br>让知识有落点，让成长看得见。',
      button: '进入实训中心',
      to: '/student/practicums',
      small: '任务驱动 / 场景实践 / 成果反馈',
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
    // 注意：已登录学生不再强制跳回 /student，否则学生端「主站」按钮
    // （routerLink="/"）会看起来"点不动"（跳过去又被弹回来）。
    // 首页模板已支持登录态（显示「学生门户」入口），学生可自行往返两端。
    if (this.isLoggedIn && this.isTeacher) {
      const returnUrl = this.route.snapshot.queryParams['returnUrl'];
      if (!returnUrl) this.router.navigate(['/admin/workbench']);
      return;
    }

    const cu = this.config.getDeep('currentUser') as Record<string, unknown> | undefined;
    if (typeof cu?.['userName'] === 'string') this.userName.set(cu['userName'] as string);

    // 从详情页“返回首页”时携带 section=<区块id>，回到首页原来的模块位置。
    // 合法取值：tenants / featured-courses / micro-majors / latest-resources / latest-news / browse。
    // 兼容旧链接 ?section=microMajors 与 ?tab=microMajors（统一映射到 micro-majors，不再去底部“全部资源”）。
    const rawSection = this.route.snapshot.queryParamMap.get('section');
    const section = rawSection === 'microMajors' ? 'micro-majors' : rawSection;
    const validSections = new Set(['tenants', 'featured-courses', 'micro-majors', 'latest-resources', 'latest-news', 'browse']);
    if (section && validSections.has(section)) {
      // section=browse（底部“全部资源”：课程/资源/微专业 Tab）返回时只恢复 tab，
      // 不再自动滑动定位到底部，停留在首页顶部。
      if (section === 'browse') {
        const browseTab = this.route.snapshot.queryParamMap.get('tab');
        if (browseTab === 'courses' || browseTab === 'resources' || browseTab === 'microMajors') {
          this.activeTab.set(browseTab);
        }
      } else {
        this.scrollToSection(section);
      }
    } else {
      const browseTab = this.route.snapshot.queryParamMap.get('tab');
      if (browseTab === 'courses' || browseTab === 'resources' || browseTab === 'microMajors') {
        this.activeTab.set(browseTab);
        if (browseTab === 'microMajors') {
          this.scrollToSection('micro-majors');
        }
        // courses/resources（底部“全部资源”）返回时不再滑动到 browse，停留在顶部。
      }
    }

    this.portal.getPublicHomeStats().subscribe(d => this.stats.set(d));
    this.portal.getPublicTenantList().subscribe({
      next: ts => {
        this.tenants.set(ts || []);
        this.loadGlobalHomeData(ts || []);
      },
      // 拉取失败也要结束骨架态，否则首屏会一直停在 loading
      error: () => this.loadingHome.set(false),
    });

    // 资源排行榜：跨所有租户取下载量最高的前 5 项
    this.portal.getTopResourcesByDownload(5).subscribe(d => this.topResources.set(d || []));

    // 首页底部「全部资源」区块已下线，不再拉取该区块数据（相关状态与方法保留，可随时恢复）
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

  /** 租户切换：专业是租户级数据，旧租户的专业选择直接失效，必须清空，否则会用串租户的专业 id 查询 */
  onTenantChange(): void {
    this.filterMajorId.set(null);
    this.onFilterChange();
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
  /** home-v2 轮播：上一张 / 下一张 */
  heroPrev(): void { this.setHeroSlide((this.heroIndex() - 1 + this.heroSlides.length) % this.heroSlides.length); }
  heroNext(): void { this.setHeroSlide((this.heroIndex() + 1) % this.heroSlides.length); }
  /** 暂停 / 播放自动轮播（对应原型 hero-pause 按钮） */
  toggleHeroPause(): void {
    const next = !this.heroPaused();
    this.heroPaused.set(next);
    if (next) this.stopHeroAutoplay();
    else this.startHeroAutoplay();
  }
  private startHeroAutoplay() {
    if (this.heroPaused()) return;
    this.stopHeroAutoplay();
    this.heroTimer = setInterval(() => this.heroIndex.update(i => (i + 1) % this.heroSlides.length), 6500);
  }
  private stopHeroAutoplay() { if (this.heroTimer) { clearInterval(this.heroTimer); this.heroTimer = null; } }
  private restartHeroAutoplay() { if (!this.heroPaused()) { this.stopHeroAutoplay(); this.startHeroAutoplay(); } }

  /** 轮播计数器：01 / 03 */
  heroCounter(): string {
    return String(this.heroIndex() + 1).padStart(2, '0');
  }

  /**
   * 滚动到首页指定模块（微专业详情页返回首页时定位到“微专业”模块）。
   *
   * 不能按固定时间重试：首页数据未就绪时 `.home-content.is-loading` 会把除骨架屏外的
   * 所有区块设为 display:none，而 scrollIntoView 对 display:none 的元素是空操作。
   * 数据加载慢于最后一次重试时，就变成完全不滚动（原来的 [100,500,1200,2500] 会在
   * 2.5s 内全部打空）。改成轮询等待区块真正渲染出来（有布局尺寸）再滚，超时才放弃。
   */
  private scrollToSection(elementId: string): void {
    const deadline = Date.now() + 10000;
    let settled = 0;

    const attempt = () => {
      const el = document.getElementById(elementId);
      // getClientRects() 为空说明元素（或祖先）处于 display:none，此时滚动无意义
      if (el && el.getClientRects().length > 0) {
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        // 卡片/图片是异步加载的，高度会后发变化；在渲染完成后补两次修正滚动
        if (settled < 2) {
          settled++;
          window.setTimeout(() => el.scrollIntoView({ behavior: 'smooth', block: 'start' }), settled * 400);
        }
        return;
      }
      if (Date.now() < deadline) {
        window.setTimeout(attempt, 200);
      }
    };

    attempt();
  }

  /**
   * 滚动到“全部资源”区域（课程/资源 tab 返回时定位）。
   * 同 scrollToSection：需等区块真正渲染出来再滚，否则 display:none 下是空操作。
   *
   * 注意：首页「全部资源」区块已下线，暂无对应 DOM；保留以备恢复。
   */
  private scrollToBrowse(): void {
    this.scrollToSection('browse');
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

  /** 资讯日期：日（原型 news-feature 的 b） */
  newsDay(value?: string | null): string {
    if (!value) return '--';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return '--';
    return String(d.getDate()).padStart(2, '0');
  }

  /** 资讯日期：年 / 月（原型 news-feature 的 small） */
  newsMonth(value?: string | null): string {
    if (!value) return '';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return '';
    return `${d.getFullYear()} / ${String(d.getMonth() + 1).padStart(2, '0')}`;
  }

  login() { this.authService.navigateToLogin(); }
  logout() { this.authService.logout().subscribe(); }

  /**
   * 「实训入口」 CTA 点击处理：
   *   - 已登录：跳转学生实训列表（/student/practicums）；
   *   - 未登录：走 OAuth 登录页，登录成功后路由守卫会在导航后带回到工作台。
   * 与 hero / 顶部右上「进入学习空间」按钮一致的语义。
   */
  goToPracticum(): void {
    if (this.isLoggedIn) {
      this.router.navigate(['/student/practicums']);
    } else {
      this.login();
    }
  }
}
