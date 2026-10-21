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
import { HomeTopNavComponent } from '../shared/home-top-nav.component';
import { LibraryCardComponent, LibraryCardVm } from '../shared/library-card.component';

/** 公开内容页分区（每个分区一个独立路由：/explore/{section}） */
export type ExploreSection = 'library' | 'courses' | 'resources' | 'micro';

const SECTION_KEYS: ExploreSection[] = ['library', 'courses', 'resources', 'micro'];

/**
 * 公开内容页（资源库 / 课程 / 资源 / 微专业各一个独立页面）。
 *
 * 首页各板块的「查看全部」直接指向对应页面，不再跳学生端页面；
 * 顶部导航与资源库卡片复用首页的共享组件，样式完全一致。
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
    LibraryCardComponent,
  ],
  templateUrl: './explore.component.html',
  styleUrls: ['./explore.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExploreComponent implements OnInit {
  private readonly portal = inject(PortalService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  /** 当前分区（来自路由 /explore/:section） */
  readonly section = signal<ExploreSection>('courses');
  readonly loading = signal(false);
  readonly browse = signal<PublicBrowseDto | null>(null);
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
      case 'library':
        return 'tenants';
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
  readonly browseMicroMajors = computed<PublicMicroMajorDto[]>(() => this.browse()?.microMajors ?? []);
  readonly filterTenants = computed<PublicBrowseFilterOption[]>(() => this.browse()?.tenants ?? []);
  readonly filterMajors = computed<PublicBrowseFilterOption[]>(() => this.browse()?.majors ?? []);

  /** 当前页总条数 */
  readonly total = computed<number>(() => {
    const data = this.browse();
    switch (this.section()) {
      case 'courses':
        return data?.totalCourseCount ?? 0;
      case 'resources':
        return data?.totalResourceCount ?? 0;
      case 'micro':
        return data?.totalMicroMajorCount ?? 0;
      default:
        return this.libraryCards().length;
    }
  });

  /** 页头标题 / 副标题 / 搜索框占位符 */
  readonly headTitle = computed<string>(() => {
    switch (this.section()) {
      case 'library':
        return '专业资源库';
      case 'courses':
        return '探索课程';
      case 'resources':
        return '教学资源';
      default:
        return '探索微专业';
    }
  });

  readonly headDesc = computed<string>(() => {
    switch (this.section()) {
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
    switch (this.section()) {
      case 'library':
        return '搜索资源库名称…';
      case 'courses':
        return '搜索课程名称…';
      case 'resources':
        return '搜索资源名称…';
      default:
        return '搜索微专业…';
    }
  });

  /**
   * 资源库卡片：租户列表 + 该租户下的课程（取浏览数据里的课程做展示）。
   * 资源库页本身就是按租户展示，因此搜索在前端按机构名/描述过滤（后端 browse 接口不按关键字过滤租户）。
   */
  readonly libraryCards = computed<LibraryCardVm[]>(() => {
    const tenants = this.tenants() || [];
    const courses = this.browseCourses() || [];
    const keyword = (this.filterSearch() || '').trim().toLowerCase();
    const matched = keyword
      ? tenants.filter(t => {
          const name = (t.tenantName || t.name || '').toLowerCase();
          const desc = (t.description || t.tenantDescription || '').toLowerCase();
          return name.includes(keyword) || desc.includes(keyword);
        })
      : tenants;

    return matched.map((t, i) => {
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
    const raw = this.route.snapshot.paramMap.get('section') as ExploreSection | null;
    if (raw && SECTION_KEYS.includes(raw)) {
      this.section.set(raw);
    } else {
      // 非法分区：回到课程页
      this.router.navigate(['/explore/courses'], { replaceUrl: true });
      this.section.set('courses');
    }

    this.portal.getPublicTenantList().subscribe({
      next: list => this.tenants.set(list || []),
      error: () => this.tenants.set([]),
    });

    this.load();
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
    this.loading.set(true);

    // 资源库页：一次取回机构列表 + 一批课程（用于卡片里的精选课程展示）。
    // 搜索在前端过滤机构名/描述，不传给后端（避免把卡片里的精选课程也过滤掉）。
    if (section === 'library') {
      this.portal
        .getPublicBrowse(undefined, undefined, undefined, 0, 60)
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
