import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzMessageService } from 'ng-zorro-antd/message';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MicroMajorService } from '../../micro-majors/micro-major.service';
import type {
  MicroMajorDto,
  MicroMajorCourseDto,
} from '../../micro-majors/micro-major.service';
import { ClientCacheService } from '../../shared/cache/client-cache.service';

/** 排序方式（对齐原型 sort select：综合 / 最新更新 / 名称） */
type SortKey = 'default' | 'updated' | 'name';

@Component({
  selector: 'app-student-micro-majors',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzPaginationModule,
  ],
  templateUrl: './student-micro-majors.component.html',
  styleUrls: ['./student-micro-majors.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMicroMajorsComponent implements OnInit {
  private readonly microMajorService = inject(MicroMajorService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly cache = inject(ClientCacheService);

  readonly items = signal<MicroMajorDto[]>([]);
  /** 微专业 id -> 课程列表（卡片展示课程名并可跳课程详情） */
  readonly courseMap = signal<Record<string, MicroMajorCourseDto[]>>({});
  readonly loading = signal(false);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(12);

  /** 关键词搜索（客户端过滤） */
  readonly filter = signal('');
  /** 行业方向筛选 */
  readonly industryFilter = signal('全部');
  /** 排序（对齐原型 sort select） */
  readonly sortKey = signal<SortKey>('default');

  /** 行业方向选项（'全部' 恒在首位） */
  readonly industryFilters = computed(() => {
    const set = new Set<string>();
    for (const it of this.items()) {
      const field = (it.industryField || '').trim();
      if (field) set.add(field);
    }
    return ['全部', ...Array.from(set).sort((a, b) => a.localeCompare(b, 'zh-Hans-CN'))];
  });

  /** 过滤后的列表（搜索 + 行业方向） */
  readonly filteredItems = computed(() => {
    const q = this.filter().trim().toLowerCase();
    const industry = this.industryFilter();
    return this.items().filter((it) => {
      if (industry !== '全部' && (it.industryField || '').trim() !== industry) return false;
      if (!q) return true;
      const haystack = `${it.title ?? ''} ${it.summary ?? ''} ${it.industryField ?? ''}`.toLowerCase();
      return haystack.includes(q);
    });
  });

  /** 排序后的列表 */
  readonly sortedItems = computed(() => {
    const list = this.filteredItems();
    const key = this.sortKey();
    if (key === 'name') {
      return list.slice().sort((a, b) => (a.title || '').localeCompare(b.title || '', 'zh-Hans-CN'));
    }
    if (key === 'updated') {
      const t = (m: MicroMajorDto) => {
        const v = (m as any).lastModificationTime || m.creationTime;
        const ms = v ? new Date(v).getTime() : 0;
        return isNaN(ms) ? 0 : ms;
      };
      return list.slice().sort((a, b) => t(b) - t(a));
    }
    return list;
  });

  /** 当前页列表（客户端分页，每页 12，网格三列） */
  readonly pagedItems = computed(() => {
    const list = this.sortedItems();
    const start = (this.pageIndex() - 1) * this.pageSize();
    return list.slice(start, start + this.pageSize());
  });

  ngOnInit(): void {
    this.loadItems();
  }

  loadItems(): void {
    this.loading.set(true);
    // 一次取足够大的一页：列表在客户端过滤/排序/分页
    this.cache.load<any>('student.micro-majors', 'list:all', () => this.microMajorService.getPublished({
      skipCount: 0,
      maxResultCount: 200,
    })).subscribe({
      next: result => {
        const list = result.items || [];
        this.items.set(list);
        this.loading.set(false);
        this.loadCourseLists(list);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载微专业列表失败');
      },
    });
  }

  /** 并行拉取当前列表各微专业的课程（卡片展示课程名并可进入课程详情） */
  private loadCourseLists(list: MicroMajorDto[]): void {
    const targets = list.filter(m => m.id);
    if (targets.length === 0) {
      this.courseMap.set({});
      return;
    }
    forkJoin(
      targets.map(m =>
        this.cache.load<any>('student.micro-majors', `detail:${m.id}`, () => this.microMajorService.getDetail(m.id!))
          .pipe(catchError(() => of(null)))
      )
    ).subscribe(results => {
      const map: Record<string, MicroMajorCourseDto[]> = {};
      results.forEach((detail, i) => {
        const id = targets[i].id!;
        if (detail?.courses?.length) map[id] = detail.courses;
      });
      this.courseMap.set(map);
    });
  }

  setSort(value: SortKey): void {
    this.sortKey.set(value);
    this.pageIndex.set(1);
  }

  selectIndustry(f: string): void {
    if (this.industryFilter() === f) return;
    this.industryFilter.set(f);
    this.pageIndex.set(1);
  }

  onSearch(): void {
    this.pageIndex.set(1);
  }

  onPageChange(pageIndex: number): void {
    this.pageIndex.set(pageIndex);
  }

  resetFilters(): void {
    this.filter.set('');
    this.industryFilter.set('全部');
    this.sortKey.set('default');
    this.pageIndex.set(1);
  }

  openDetail(item: MicroMajorDto): void {
    this.router.navigate(['/student/micro-majors', item.id]);
  }

  /** 卡片课程名 → 课程详情（透传 fromMicroMajor，课程页可返回微专业） */
  openCourse(item: MicroMajorDto, course: MicroMajorCourseDto, event: Event): void {
    event.stopPropagation();
    if (!course.courseId) return;
    this.router.navigate(['/student/courses', course.courseId], {
      queryParams: { fromMicroMajor: item.id, from: 'micro-majors' },
    });
  }

  hasCover(item: MicroMajorDto): boolean {
    return !!item.coverImageUrl && item.coverImageUrl.trim().length > 0;
  }

  /** 封面色块 c0-c3：按标题 hash 稳定取色（对齐原型 cover 色块） */
  coverClass(item: MicroMajorDto, index: number): number {
    const key = item.title || item.id || `${index}`;
    let sum = 0;
    for (let i = 0; i < key.length; i++) sum = (sum + key.charCodeAt(i)) % 4;
    return sum;
  }

  /** 卡片课程列表 */
  cardCourses(item: MicroMajorDto): MicroMajorCourseDto[] {
    return (item.id && this.courseMap()[item.id]) || [];
  }

  /** 卡片上最多展示 3 门课程名 */
  cardPreviewCourses(item: MicroMajorDto): MicroMajorCourseDto[] {
    return this.cardCourses(item).slice(0, 3);
  }

  /** 卡片底部课程数：优先真实 courseCount */
  courseCountText(item: MicroMajorDto): string {
    const n = item.courseCount || this.cardCourses(item).length || 0;
    return `${n} 门课程`;
  }

  /** 卡片摘要 */
  summaryText(item: MicroMajorDto): string {
    return (item.summary || '').trim() || '系统化课程体系，理论与实践并重。';
  }
}
