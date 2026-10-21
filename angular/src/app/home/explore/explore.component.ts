import { ChangeDetectionStrategy, Component, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
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
import { BrandingService } from '../../shared/branding/branding.service';

/** 公开内容浏览页的分区 */
export type ExploreTab = 'library' | 'courses' | 'resources' | 'micro';

/** 资源库卡片（租户 + 该租户下的精选课程） */
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
  courses: { id: string; title: string }[];
}

/**
 * 公开内容探索页（资源库 / 课程 / 资源 / 微专业）。
 *
 * 首页各板块的「查看全部」都指向这里，不再跳学生端页面；
 * 卡片结构与点击行为与首页保持一致（课程→课程预览、资源→在线预览、微专业→培养路径、机构→资源库）。
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
  ],
  templateUrl: './explore.component.html',
  styleUrls: ['./explore.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExploreComponent implements OnInit {
  private readonly portal = inject(PortalService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly branding = inject(BrandingService);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  readonly tabs: { key: ExploreTab; label: string; icon: string }[] = [
    { key: 'library', label: '资源库', icon: 'appstore' },
    { key: 'courses', label: '课程', icon: 'book' },
    { key: 'resources', label: '资源', icon: 'folder' },
    { key: 'micro', label: '微专业', icon: 'deployment-unit' },
  ];

  readonly activeTab = signal<ExploreTab>('courses');
  readonly loading = signal(false);
  readonly browse = signal<PublicBrowseDto | null>(null);
  readonly tenants = signal<TenantResourceSummaryDto[]>([]);

  /** 每页条数 */
  readonly pageSize = 9;
  /** 各分区独立分页（后端按当前分区分页拉取） */
  readonly pages = signal<Record<ExploreTab, number>>({
    library: 1,
    courses: 1,
    resources: 1,
    micro: 1,
  });

  readonly filterTenantId = signal<string | null>(null);
  readonly filterMajorId = signal<string | null>(null);
  readonly filterSearch = signal('');

  /** 「公共课」选项：课程未归属任何专业 */
  readonly publicOnlyValue = '__public__';

  private readonly tones: Array<'sage' | 'blue' | 'sand'> = ['sage', 'blue', 'sand'];
  private readonly toneIcons = ['read', 'thunderbolt', 'appstore'];

  readonly browseCourses = computed<PublicCourseDto[]>(() => this.browse()?.courses ?? []);
  readonly browseResources = computed<PublicResourceDto[]>(() => this.browse()?.resources ?? []);
  readonly browseMicroMajors = computed<PublicMicroMajorDto[]>(() => this.browse()?.microMajors ?? []);
  readonly filterTenants = computed<PublicBrowseFilterOption[]>(() => this.browse()?.tenants ?? []);
  readonly filterMajors = computed<PublicBrowseFilterOption[]>(() => this.browse()?.majors ?? []);

  /** 当前分区的总条数 */
  readonly total = computed<number>(() => {
    const data = this.browse();
    switch (this.activeTab()) {
      case 'courses':
        return data?.totalCourseCount ?? 0;
      case 'resources':
        return data?.totalResourceCount ?? 0;
      case 'micro':
        return data?.totalMicroMajorCount ?? 0;
      default:
        return this.tenants().length;
    }
  });

  /** 当前分区分页 */
  readonly currentPage = computed<number>(() => this.pages()[this.activeTab()]);

  /** 页头标题 / 副标题 / 搜索框占位符（随分区变化） */
  readonly headTitle = computed<string>(() => {
    switch (this.activeTab()) {
      case 'library':
        return '探索资源库';
      case 'courses':
        return '探索课程';
      case 'resources':
        return '发现资源';
      default:
        return '探索微专业';
    }
  });

  readonly headDesc = computed<string>(() => {
    switch (this.activeTab()) {
      case 'library':
        return '按专业汇聚课程与配套资源，先找到方向，再开始学习。';
      case 'courses':
        return '找到适合自己的学习方向。';
      case 'resources':
        return '课件、文档、视频与素材，按机构与专业快速定位。';
      default:
        return '不只是一门课，而是一条清晰、系统的成长路径。';
    }
  });

  readonly searchPlaceholder = computed<string>(() => {
    switch (this.activeTab()) {
      case 'library':
        return '搜索资源库…';
      case 'courses':
        return '搜索课程名称…';
      case 'resources':
        return '搜索资源名称…';
      default:
        return '搜索微专业…';
    }
  });

  /** 资源库卡片：租户列表 + 该租户下的课程（取浏览数据里的课程做展示） */
  readonly libraryCards = computed<LibraryCardVm[]>(() => {
    const tenants = this.tenants() || [];
    const courses = this.browseCourses() || [];
    return tenants.map((t, i) => {
      const own = courses.filter(c => c.tenantId === t.id);
      return {
        id: t.id,
        name: t.tenantName || t.name || '资源库',
        desc: t.description || t.tenantDescription || '汇聚专业课程与配套资源，先找到方向，再开始学习。',
        tone: this.tones[i % this.tones.length],
        icon: this.toneIcons[i % this.toneIcons.length],
        num: String(i + 1).padStart(2, '0'),
        courseCount: t.courseCount || 0,
        resourceCount: t.resourceCount || 0,
        microMajorCount: t.microMajorCount || 0,
        courses: own.slice(0, 3).map(c => ({ id: c.id as string, title: c.title || '' })),
      };
    });
  });

  ngOnInit(): void {
    this.branding.ensureLoaded();

    const tab = this.route.snapshot.queryParamMap.get('tab') as ExploreTab | null;
    if (tab && this.tabs.some(t => t.key === tab)) {
      this.activeTab.set(tab);
    }

    this.portal.getPublicTenantList().subscribe({
      next: list => this.tenants.set(list || []),
      error: () => this.tenants.set([]),
    });

    this.load();
  }

  /** 切换分区（同步到 URL，便于分享 / 刷新保持） */
  setTab(tab: ExploreTab): void {
    if (this.activeTab() === tab) return;
    this.activeTab.set(tab);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    this.load();
  }

  /** 筛选条件变化：回到第一页重新拉取 */
  onFilterChange(): void {
    this.pages.update(p => ({ ...p, [this.activeTab()]: 1 }));
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
    this.pages.update(p => ({ ...p, [this.activeTab()]: page }));
    this.load();
  }

  /** 拉取当前分区的数据 */
  private load(): void {
    const tab = this.activeTab();
    this.loading.set(true);

    // 资源库分区：一次取回机构列表 + 一批课程（用于卡片里的精选课程展示）
    if (tab === 'library') {
      this.portal
        .getPublicBrowse(
          this.filterTenantId() || undefined,
          undefined,
          this.filterSearch() || undefined,
          0,
          60
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
      return;
    }

    const page = this.pages()[tab];
    const skipCount = (page - 1) * this.pageSize;
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
