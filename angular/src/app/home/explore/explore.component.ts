import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSpinModule } from 'ng-zorro-antd/spin';

import { PortalService } from '../../proxy/portal/portal.service';
import type {
  PublicBrowseDto,
  PublicBrowseFilterOption,
  PublicCourseDto,
  PublicMicroMajorDto,
  PublicResourceDto,
  TenantResourceSummaryDto,
} from '../../proxy/portal/models';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { HomeTopNavComponent } from '../shared/home-top-nav.component';
import { buildMajorOptions } from '../shared/major-options';
import type { MajorOptionVm } from '../shared/major-options';
import { BrandingService } from '../../shared/branding/branding.service';
import {
  TeachingLibraryCatalogService,
  type TeachingLibraryCard,
} from './teaching-library-catalog.service';

/**
 * 公开内容页分区（每个分区一个独立路由：/explore/{section}）。
 *
 * <para>「资源库」分区已独立成 <c>/explore/library</c>（多校共建专业库列表），
 * 不再由本组件承载 —— 见 app.routes.ts 中更靠前的静态路由。</para>
 */
export type ExploreSection = 'courses' | 'resources' | 'micro';

/** 微专业卡片视图（补充后端新字段：培养目标、总学时、课程链路） */
export type MicroMajorCard = PublicMicroMajorDto & {
  summary?: string | null;
  hours?: number;
  courseTitles?: string[];
};

const SECTION_KEYS: ExploreSection[] = ['courses', 'resources', 'micro'];

/**
 * 公开内容页（教学资源库 / 资源 / 微专业各一个独立页面）。
 *
 * <para>
 * <b>courses 分区已从「课程列表」改为「教学资源库列表」</b>（对齐原型 #courses）：
 * 校内资源库（本校主建的私有 / 公开 / 共建库）与公共共享资源库（他校公开或共建的库）
 * 两个分区，数据来自 `GET /api/app/teaching-library-catalog`，卡片点进去即库详情 `/library/{id}`。
 * 「共建共享资源库」另有一个独立列表页 `/explore/library`。
 * </para>
 */
@Component({
  selector: 'app-explore',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzInputModule,
    NzSelectModule,
    NzPaginationModule,
    NzEmptyModule,
    NzSpinModule,
    FilePreviewComponent,
    HomeTopNavComponent,
  ],
  templateUrl: './explore.component.html',
  styleUrls: ['./explore.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExploreComponent implements OnInit, OnDestroy {
  private readonly portal = inject(PortalService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly catalog = inject(TeachingLibraryCatalogService);
  private readonly branding = inject(BrandingService);

  /** 页脚品牌名（品牌配置的站点标题） */
  readonly footerBrand = computed<string>(() => (this.branding.appTitle() || '易课通').trim());

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  /** 路由参数订阅的销毁标记 */
  private readonly destroy$ = new Subject<void>();

  /** 当前分区（来自路由 /explore/:section） */
  readonly section = signal<ExploreSection>('courses');
  readonly loading = signal(false);
  readonly browse = signal<PublicBrowseDto | null>(null);

  /**
   * 教学资源库目录（courses 分区已从「课程列表」改为「资源库列表」）。
   * 两个分区：school 校内（本校主建的私有/公开/共建库）、public 公共共享（他校公开或共建）。
   */
  readonly libraryScope = signal<'all' | 'school' | 'public'>('all');
  readonly libraries = signal<TeachingLibraryCard[]>([]);
  readonly libraryTotal = signal(0);
  readonly librariesLoading = signal(false);
  /** 资源库分区的专业下拉选项 */
  readonly libraryMajors = signal<PublicBrowseFilterOption[]>([]);
  /** 专业下拉展示项：同名跨校时追加学校后缀 */
  readonly libraryMajorOptions = computed<MajorOptionVm[]>(() =>
    buildMajorOptions(this.libraryMajors()),
  );
  readonly tenants = signal<TenantResourceSummaryDto[]>([]);

  /** 每页条数 */
  readonly pageSize = 9;
  /** 分页（每个分区独立） */
  readonly page = signal(1);

  readonly filterTenantId = signal<string | null>(null);
  readonly filterMajorId = signal<string | null>(null);
  readonly filterSearch = signal('');

  /** 「公共课」选项：课程未归属任何专业 */
  readonly publicOnlyValue = '__public__';

  /**
   * 返回首页时定位的栏目锚点（首页对应区块的 id）：
   * 资源库→tenants、课程→featured-courses、资源→latest-resources、微专业→micro-majors
   */
  readonly homeSection = computed<string>(() => {
    switch (this.section()) {
      case 'courses':
        return 'featured-courses';
      case 'resources':
        return 'latest-resources';
      default:
        return 'micro-majors';
    }
  });

  private readonly tones: Array<'sage' | 'blue' | 'sand'> = ['sage', 'blue', 'sand'];
  private readonly toneIcons = ['read', 'thunderbolt', 'appstore'];

  readonly browseCourses = computed<PublicCourseDto[]>(() => this.browse()?.courses ?? []);
  readonly browseResources = computed<PublicResourceDto[]>(() => this.browse()?.resources ?? []);
  /** 微专业卡片：后端新增摘要 / 学时 / 课程链路（proxy 待下次重生成，这里用交叉类型承载） */
  readonly browseMicroMajors = computed<MicroMajorCard[]>(
    () => (this.browse()?.microMajors ?? []) as MicroMajorCard[],
  );
  readonly filterTenants = computed<PublicBrowseFilterOption[]>(() => this.browse()?.tenants ?? []);
  readonly filterMajors = computed<PublicBrowseFilterOption[]>(() => this.browse()?.majors ?? []);

  /** 当前页总条数 */
  readonly total = computed<number>(() => {
    const data = this.browse();
    switch (this.section()) {
      case 'courses':
        return this.libraryTotal();
      case 'resources':
        return data?.totalResourceCount ?? 0;
      case 'micro':
        return data?.totalMicroMajorCount ?? 0;
      default:
        return 0;
    }
  });

  /** 页头标题 / 副标题 / 搜索框占位符 */
  readonly headTitle = computed<string>(() => {
    switch (this.section()) {
      case 'courses':
        return '教学资源库';
      case 'resources':
        return '教学资源';
      default:
        return '探索微专业';
    }
  });

  readonly headDesc = computed<string>(() => {
    switch (this.section()) {
      case 'courses':
        return '按专业和开放范围选择资源库：校内资源库面向本校，公共共享资源库支持跨校学习。';
      case 'resources':
        return '课件、文档、视频与素材，按机构与专业快速定位。';
      default:
        return '不只是一门课，而是一条清晰、系统的成长路径。';
    }
  });

  readonly searchPlaceholder = computed<string>(() => {
    switch (this.section()) {
      case 'courses':
        return '搜索资源库名称 / 专业 / 主建学校…';
      case 'resources':
        return '搜索资源名称…';
      default:
        return '搜索微专业…';
    }
  });

  ngOnInit(): void {
    this.applySection(this.route.snapshot.paramMap.get('section'), false);

    // 首页顶部搜索框带过来的筛选条件：?q=关键字 &major=专业Id
    // （首页 searchbar → /explore/courses?q=…&major=…）
    this.applyQueryFilters();

    // 顶部 Tab 在 /explore/:section 之间跳转时，Angular 会复用同一个组件实例
    // （路由配置相同、只变了参数），ngOnInit 不会再次执行。
    // 这里订阅参数变化，切换分区时重置筛选/分页并重新拉数据，
    // 否则会出现「地址栏已经变成 /explore/courses，页面内容还停在专业资源库」。
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(pm => {
      this.applySection(pm.get('section'), true);
    });

    this.portal.getPublicTenantList().subscribe({
      next: list => this.tenants.set(list || []),
      error: () => this.tenants.set([]),
    });

    // 资源库分区的「专业方向」下拉数据源（与资源/微专业分区的 browse().majors 同源）
    this.portal.getPublicBrowse(undefined, undefined, undefined, 0, 1).subscribe({
      next: data => this.libraryMajors.set(data?.majors || []),
      error: () => this.libraryMajors.set([]),
    });

    this.load();
  }

  /**
   * 应用路由上的分区。
   * @param raw 路由参数（courses / resources / micro）
   * @param reload 是否在分区变化时重置筛选并重新加载（首次初始化时由 ngOnInit 统一加载）
   */
  private applySection(raw: string | null | undefined, reload: boolean): void {
    const next = raw as ExploreSection | null;
    if (!next || !SECTION_KEYS.includes(next)) {
      // 非法分区：回到课程页
      this.router.navigate(['/explore/courses'], { replaceUrl: true });
      this.section.set('courses');
      return;
    }
    const changed = this.section() !== next;
    this.section.set(next);
    if (!changed || !reload) return;
    // 换分区：上一次的机构/专业/关键字筛选不一定适用，连同分页一起重置
    this.page.set(1);
    this.filterSearch.set('');
    this.filterTenantId.set(null);
    this.filterMajorId.set(null);
    this.load();
  }

  /**
   * 读取首页搜索框带过来的查询参数（q / major）并套用到筛选条件上。
   */
  private applyQueryFilters(): void {
    const qp = this.route.snapshot.queryParamMap;
    const q = (qp.get('q') || '').trim();
    const major = qp.get('major');
    if (q) this.filterSearch.set(q);
    if (major) this.filterMajorId.set(major);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /** 筛选条件变化：回到第一页重新拉取 */
  onFilterChange(): void {
    this.page.set(1);
    this.load();
  }

  onTenantChange(): void {
    // 换机构后专业筛选可能失效，清掉以免出现空结果
    this.filterMajorId.set(null);
    this.onFilterChange();
  }

  clearFilters(): void {
    this.filterTenantId.set(null);
    this.filterMajorId.set(null);
    this.filterSearch.set('');
    this.onFilterChange();
  }

  readonly hasActiveFilters = computed<boolean>(
    () => !!(this.filterTenantId() || this.filterMajorId() || this.filterSearch())
  );

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  /** 拉取当前页数据 */
  private load(): void {
    const section = this.section();

    // courses 分区 = 教学资源库目录（校内 / 公共共享 两个分区）
    if (section === 'courses') {
      this.loadLibraries();
      return;
    }

    this.loading.set(true);

    const skipCount = (this.page() - 1) * this.pageSize;
    const majorSel = this.filterMajorId();
    const publicOnly = majorSel === this.publicOnlyValue;

    this.portal
      .getPublicBrowse(
        this.filterTenantId() || undefined,
        !publicOnly ? majorSel || undefined : undefined,
        this.filterSearch() || undefined,
        skipCount,
        this.pageSize,
        publicOnly ? true : undefined
      )
      .subscribe({
        next: data => {
          this.browse.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.browse.set(null);
          this.loading.set(false);
        },
      });
  }

  /** 加载教学资源库目录（当前分区 + 搜索 + 分页） */
  private loadLibraries(): void {
    this.librariesLoading.set(true);
    const skipCount = (this.page() - 1) * this.pageSize;

    this.catalog
      .getList({
        scope: this.libraryScope(),
        filter: this.filterSearch() || undefined,
        primaryMajorId: this.filterMajorId() || undefined,
        skipCount,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: res => {
          this.libraries.set(res?.items || []);
          this.libraryTotal.set(res?.totalCount || 0);
          this.librariesLoading.set(false);
        },
        error: () => {
          this.libraries.set([]);
          this.libraryTotal.set(0);
          this.librariesLoading.set(false);
        },
      });
  }

  /** 切换筛选范围（全部范围 / 共享资源库 / 校内资源库）—— 对齐原型的选择器交互 */
  setLibraryScope(scope: 'all' | 'school' | 'public'): void {
    if (this.libraryScope() === scope) return;
    this.libraryScope.set(scope);
    this.page.set(1);
    this.loadLibraries();
  }

  /** 专业方向筛选（资源库按主建专业过滤） */
  setLibraryMajor(majorId: string | null | undefined): void {
    this.filterMajorId.set(majorId || null);
    this.page.set(1);
    this.loadLibraries();
  }

  /** 重置资源库筛选（关键词 / 专业 / 范围） */
  resetLibraryFilters(): void {
    this.filterSearch.set('');
    this.filterMajorId.set(null);
    this.libraryScope.set('all');
    this.page.set(1);
    this.loadLibraries();
  }

  /** 资源库列表是否为空（含加载完成判断，供空态/结果区使用） */
  readonly libraryEmpty = computed(
    () => !this.librariesLoading() && this.libraries().length === 0,
  );

  /** 微专业色块（原型 .micro-direction 的 green / blue / orange 轮换） */
  microTone(index: number): string {
    return ['green', 'blue', 'orange'][index % 3];
  }

  /** 资源库可见性标签：0 私有 / 1 公开 / 2 院校共建 */
  libraryVisibilityTag(lib: TeachingLibraryCard): string {
    return lib.visibility === 2 ? '院校共建' : lib.visibility === 1 ? '公开' : '私有';
  }

  /** 「公共共享」卡片上的主建学校文案 */
  libraryOwnerText(lib: TeachingLibraryCard): string {
    return lib.ownerTenantName || '未知学校';
  }

  // ── 卡片展示辅助（与首页保持一致的视觉/交互）────────────────

  coverTone(index: number): string {
    return ['c0', 'c1', 'c2', 'c3'][index % 4];
  }

  coverTitle(title?: string | null): string {
    const t = (title || '').trim();
    return t.length > 12 ? t.slice(0, 12) + '…' : t;
  }

  /** 课程卡片的专业文本：多专业用「、」连接，无归属显示「公共课」 */
  courseMajorText(c: PublicCourseDto | null | undefined): string {
    const names = (c?.majorNames || []).filter(n => !!n);
    if (names.length > 0) return names.join('、');
    return c?.majorName || '公共课';
  }

  /** 课程简介（列表页摘要，超长截断） */
  courseDesc(c: PublicCourseDto | null | undefined): string {
    const d = (c?.description || '').trim();
    if (!d) return '系统化课程内容，学以致用。';
    return d.length > 60 ? d.slice(0, 60) + '…' : d;
  }

  fileExtLabel(ext?: string | null): string {
    return (ext || 'FILE').toUpperCase().replace(/^\./, '');
  }

  isVideo(ext?: string | null): boolean {
    const e = (ext || '').toLowerCase();
    return e.includes('mp4') || e.includes('avi') || e.includes('mov') || e.includes('flv') || e.includes('mkv');
  }

  /** 根据文件扩展名返回对应的图标名 */
  materialIcon(ext?: string | null): string {
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

  /** 资源是否可在线预览 */
  canPreview(m: PublicResourceDto | null | undefined): boolean {
    return !!(m?.id && (m.fileExtension || (m.originalFileName && m.originalFileName.includes('.')) || (m.fileSize || 0) > 0));
  }

  /** 打开资源预览（与首页一致） */
  previewMaterial(m: PublicResourceDto): void {
    if (!m.id) return;
    if (!m.fileExtension && !m.originalFileName && !m.fileSize) return;
    let ext = m.fileExtension || '';
    if (!ext) {
      const fileName = m.originalFileName || m.name || '';
      const dot = fileName.lastIndexOf('.');
      if (dot >= 0) ext = fileName.substring(dot);
    }
    this.filePreview.open(m.id, m.originalFileName || m.name || '', ext, m.fileSize || 0);
  }
}
