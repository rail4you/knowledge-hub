import { Component, ChangeDetectionStrategy, computed, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterModule, ActivatedRoute } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasAnyRole, hasRole } from '../auth/current-user.utils';
import { ADMIN_ROLES } from '../auth/admin-roles';
import { PortalService } from '../proxy/portal/portal.service';
import type {
  PublicHomeStatsDto,
  TenantResourceSummaryDto,
  PublicBrowseDto,
  PublicResourceLibraryDto,
  CourseBriefDto,
  MicroMajorBriefDto,
} from '../proxy/portal/models';
import { BrandingService } from '../shared/branding/branding.service';
import { HomeTopNavComponent } from './shared/home-top-nav.component';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

/**
 * 首页轮播幻灯片（严格对齐原型 newHome 的 slider）。
 */
interface HeroSlide {
  /** 小标签 */
  label: string;
  /** 主标题 */
  title: string;
  /** 描述 */
  desc: string;
  /** CTA 文案 */
  button: string;
  /** CTA 跳转路由 */
  to: string;
  /** 插画类型：learning / shared / micro（对应原型的三张 SVG 示意图） */
  art: 'learning' | 'shared' | 'micro';
}

/** 首页「微专业」板块的一行（对齐原型 home-program）。 */
interface HomeProgramVm {
  id: string;
  name: string;
  /** 一句话简介 */
  intro: string;
  /** 课程名列表（最多 3 个 + 省略号） */
  courses: string[];
  /** 课程总数 */
  courseCount: number;
  /** 合计学时（来自课程真实学时） */
  hours: number;
  /** 建设单位 */
  school: string;
  /** 负责教师 */
  teacher: string;
  /** 封面色调 */
  tone: 'green' | 'blue' | 'orange';
}

/** 首页课程卡片（推荐课程 / 已发布课程 两种来源的统一视图） */
interface HomeCourseVm {
  id: string;
  title: string;
  coverImageUrl?: string | null;
  teacherName?: string | null;
  majorName?: string | null;
  studentCount?: number;
  semesterHours?: number;
  /** 建设单位（学校） */
  tenantName: string;
}

/** 首页跨租户聚合的课程 */
interface GlobalCourseItem extends CourseBriefDto {
  tenantId: string;
  tenantName: string;
}
/** 首页跨租户聚合的微专业 */
interface GlobalMicroMajorItem extends MicroMajorBriefDto {
  tenantId: string;
  tenantName: string;
}

@Component({
  selector: 'app-portal-home',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NzIconModule, NzSelectModule, HomeTopNavComponent],
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

  // ── 首页跨租户聚合内容（全景展示） ──
  readonly globalFeaturedCourses = signal<GlobalCourseItem[]>([]);
  readonly globalMicroMajors = signal<GlobalMicroMajorItem[]>([]);

  /** 共建共享资源库（多校共建专业库）—— 门户真实数据 */
  readonly libraries = signal<PublicResourceLibraryDto[]>([]);

  /** 加载态：首屏数据未就绪时为 true，用于骨架占位避免跳动 */
  readonly loadingHome = signal(true);

  // ── 首页板块数据 ──
  /** 教学资源库板块：推荐课程优先，不足 5 门时用已发布课程补齐 */
  readonly homeCourses = computed<HomeCourseVm[]>(() => {
    const featured: HomeCourseVm[] = (this.globalFeaturedCourses() || []).map(c => ({
      id: c.id as string,
      title: c.title || '',
      coverImageUrl: c.coverImageUrl,
      teacherName: c.teacherName,
      majorName: c.majorName,
      studentCount: c.studentCount,
      semesterHours: c.semesterHours,
      tenantName: c.tenantName,
    }));
    if (featured.length >= 5) return featured.slice(0, 5);
    const seen = new Set(featured.map(c => c.id));
    const extra: HomeCourseVm[] = (this.browseData()?.courses || [])
      .filter(c => !seen.has(c.id as string))
      .map(c => ({
        id: c.id as string,
        title: c.title || '',
        coverImageUrl: c.coverImageUrl,
        teacherName: c.teacherName,
        majorName: c.majorName,
        studentCount: c.studentCount,
        semesterHours: c.semesterHours,
        tenantName: c.tenantName || '',
      }));
    return [...featured, ...extra].slice(0, 5);
  });

  /** 共建共享资源库：第一个作为重点共建，其余作为推荐 */
  readonly featureLibrary = computed<PublicResourceLibraryDto | null>(() => this.libraries()[0] ?? null);
  readonly recommendLibraries = computed<PublicResourceLibraryDto[]>(() => this.libraries().slice(1, 3));

  /** 微专业板块（原型 home-programs） */
  readonly homePrograms = computed<HomeProgramVm[]>(() => {
    const majors = this.globalMicroMajors() || [];
    const courses = this.globalFeaturedCourses() || [];
    const tones: HomeProgramVm['tone'][] = ['green', 'blue', 'orange'];
    return majors.slice(0, 4).map((m, i) => {
      const own = courses.filter(c => c.tenantId === m.tenantId);
      const lessons = own.slice(0, 3);
      return {
        id: m.id as string,
        name: m.title || '微专业',
        intro: m.tenantName
          ? `${m.tenantName}建设的系统化微专业课程路径，连接课程、项目实践与能力成长。`
          : '系统化课程体系，跨学科融合培养。',
        courses: lessons.map(c => c.title || ''),
        courseCount: m.courseCount || lessons.length,
        hours: lessons.reduce((sum, c) => sum + (c.semesterHours || 0), 0),
        school: m.tenantName || '专业教学团队',
        teacher: lessons[0]?.teacherName || '专业教学团队',
        tone: tones[i % tones.length],
      };
    });
  });

  /** 专业方向（顶部搜索框下拉） */
  readonly majorOptions = computed<string[]>(() => {
    const names = (this.browseData()?.majors || []).map(m => m.name).filter(n => !!n);
    return ['全部专业', ...Array.from(new Set(names))];
  });

  /** 首页搜索框状态 */
  readonly searchMajor = signal('全部专业');
  readonly searchKeyword = signal('');

  /** 首页轮播（对齐原型 slider：三张主题图 + SVG 示意图） */
  readonly heroSlides: HeroSlide[] = [
    {
      label: '优质内容 · 便捷学习',
      title: '让好课程，走进每一间课堂',
      desc: '按专业发现课程与资源，找到适合你的学习内容。',
      button: '浏览教学资源',
      to: '/explore/courses',
      art: 'learning',
    },
    {
      label: '多校共建 · 授权共享',
      title: '汇聚多校力量，共建专业资源',
      desc: '从共同建设到课堂引用，让优质教学成果持续被使用。',
      button: '进入共建共享资源库',
      to: '/explore/library',
      art: 'shared',
    },
    {
      label: '微专业 · 系统成长',
      title: '用一组课程，拓展一种新能力',
      desc: '连接跨专业课程与实践，找到清晰的专项学习路径。',
      button: '探索微专业课程',
      to: '/explore/micro',
      art: 'micro',
    },
  ];

  /** 轮播插画：牵头学校 / 参建学校 / 共建教师 / 本校课程 */
  readonly sharedNodes: { x: number; y: number; label: string }[] = [
    { x: 90, y: 65, label: '牵头学校' },
    { x: 350, y: 65, label: '参建学校' },
    { x: 90, y: 172, label: '共建教师' },
    { x: 350, y: 172, label: '本校课程' },
  ];

  /** 轮播插画：核心课程 → 项目实践 → 专项能力 */
  readonly microBars: { x: number; y: number; h: number; label: string }[] = [
    { x: 60, y: 130, h: 85, label: '核心课程' },
    { x: 177, y: 90, h: 125, label: '项目实践' },
    { x: 294, y: 45, h: 170, label: '专项能力' },
  ];

  readonly heroIndex = signal(0);
  readonly heroPaused = signal(false);
  private heroTimer: ReturnType<typeof setInterval> | null = null;


  /** 首页课程封面色（无封面图时的占位色，对齐原型 cover 配色） */
  coverTone(index: number): string {
    return ['green', 'blue', 'orange', 'purple'][index % 4];
  }

  /**
   * 封面大字：优先用专业名（与原型的「主题标签」一致），没有专业时退回课程标题截断。
   */
  coverTitle(c: HomeCourseVm): string {
    const major = (c.majorName || '').trim();
    if (major) return major.length > 8 ? major.slice(0, 8) : major;
    const t = (c.title || '').trim();
    return t.length > 8 ? t.slice(0, 8) : t;
  }

  /**
   * 资源库封面短标签：主建专业名按 2 字一行切分（与原型 mini-art 的排版一致）。
   * @param l 资源库
   * @param from 起始字符位（0 = 前两字，2 = 后两字）
   */
  libraryCoverLabel(l: PublicResourceLibraryDto, from = 0): string {
    const name = (l.primaryMajorName || '').trim();
    if (!name) return from === 0 ? '专业' : '资源库';
    return from === 0 ? name.slice(0, 2) : name.slice(2, 4);
  }

  /** 参与建设学校摘要 */
  partnerText(l: PublicResourceLibraryDto): string {
    const partners = l.partnerNames || [];
    if (!partners.length) return '暂无参与建设学校';
    return partners.join('、');
  }

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
      if (!returnUrl) this.router.navigate(['/admin/workbench']);
      return;
    }

    const cu = this.config.getDeep('currentUser') as Record<string, unknown> | undefined;
    if (typeof cu?.['userName'] === 'string') this.userName.set(cu['userName'] as string);

    // 从详情页“返回首页”时携带 section=<区块id>，回到首页原来的模块位置。
    const rawSection = this.route.snapshot.queryParamMap.get('section');
    const validSections = new Set(['tenants', 'featured-courses', 'micro-majors', 'latest-resources', 'latest-news']);
    if (rawSection && validSections.has(rawSection)) {
      this.scrollToSection(rawSection);
    }

    this.portal.getPublicHomeStats().subscribe(d => this.stats.set(d));

    // 共建共享资源库（多校共建专业库）
    this.portal.getPublicLibraryList().subscribe({
      next: list => this.libraries.set(list || []),
      error: () => this.libraries.set([]),
    });

    // 公开浏览（跨租户）：一次取回课程 / 资源 / 微专业 / 专业筛选项
    this.portal.getPublicBrowse(undefined, undefined, undefined, 0, 60).subscribe({
      next: data => {
        this.browseData.set(data);
        this.loadingHome.set(false);
      },
      error: () => this.loadingHome.set(false),
    });

    this.portal.getPublicTenantList().subscribe({
      next: ts => {
        this.tenants.set(ts || []);
        this.loadGlobalHomeData(ts || []);
      },
      error: () => this.loadingHome.set(false),
    });

    this.startHeroAutoplay();
  }

  ngOnDestroy(): void {
    if (this.heroTimer) clearInterval(this.heroTimer);
  }

  /**
   * 首页全景展示：拉取所有有数据租户的首页数据，合并精选课程/微专业/最新资源/最新资讯。
   * 只跳过课程+资源+微专业都为 0 的空租户，避免对无人租户发起无意义的请求。
   */
  private loadGlobalHomeData(tenants: TenantResourceSummaryDto[]): void {
    const activeTenants = (tenants || []).filter(
      t => (t.courseCount || 0) + (t.resourceCount || 0) + (t.microMajorCount || 0) > 0
    );
    if (activeTenants.length === 0) {
      this.loadingHome.set(false);
      return;
    }

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

      for (const r of results) {
        const h = r.home;
        if (!h) continue;
        for (const c of h.featuredCourses || []) {
          courses.push({ ...c, tenantId: r.tenantId, tenantName: r.tenantName });
        }
        for (const m of h.microMajors || []) {
          microMajors.push({ ...m, tenantId: r.tenantId, tenantName: r.tenantName });
        }
      }

      this.globalFeaturedCourses.set(courses);
      this.globalMicroMajors.set(microMajors);
      this.loadingHome.set(false);
    });
  }

  // ── 轮播控制 ──
  setHeroSlide(index: number): void {
    this.heroIndex.set(index);
    this.restartHeroAutoplay();
  }

  heroPrev(): void {
    const n = this.heroSlides.length;
    this.setHeroSlide((this.heroIndex() - 1 + n) % n);
  }

  heroNext(): void {
    this.setHeroSlide((this.heroIndex() + 1) % this.heroSlides.length);
  }

  toggleHeroPause(): void {
    this.heroPaused.update(v => !v);
    this.restartHeroAutoplay();
  }

  private startHeroAutoplay(): void {
    if (this.heroTimer) clearInterval(this.heroTimer);
    this.heroTimer = setInterval(() => {
      if (this.heroPaused() || document.hidden) return;
      this.heroIndex.update(i => (i + 1) % this.heroSlides.length);
    }, 6000);
  }

  private restartHeroAutoplay(): void {
    this.startHeroAutoplay();
  }

  // ── 搜索 ──
  /** 顶部搜索：按专业方向 + 关键字跳到课程列表 */
  onSearch(): void {
    const q = (this.searchKeyword() || '').trim();
    const major = this.searchMajor();
    const params: Record<string, string> = {};
    if (q) params['q'] = q;
    if (major && major !== '全部专业') {
      const hit = (this.browseData()?.majors || []).find(m => m.name === major);
      if (hit) params['major'] = hit.id;
    }
    this.router.navigate(['/explore/courses'], { queryParams: params });
  }

  /** 滚动到指定板块 */
  private scrollToSection(id: string): void {
    setTimeout(() => {
      const el = document.getElementById(id);
      if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 120);
  }
}
