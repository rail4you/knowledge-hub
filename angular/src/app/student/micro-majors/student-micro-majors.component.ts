import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MicroMajorService } from '../../proxy/micro-majors/micro-major.service';
import type {
  MicroMajorCourseDto,
  MicroMajorDto,
  MicroMajorEnrollmentDto,
} from '../../proxy/micro-majors/dtos/models';
import { MicroMajorEnrollmentStatus } from '../../proxy/micro-majors/enums/micro-major-enrollment-status.enum';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import {
  MICRO_MAJOR_ENROLLMENT_LABELS,
  microMajorStatusLabel,
} from './micro-major-labels';

/** 顶部统计卡 */
interface CenterStat {
  label: string;
  value: number;
  hint: string;
}

/**
 * 学生端 · 微专业中心（原型 #micro）。
 *
 * 结构：Hero → 四项统计 → 方向筛选 + 关键词 → 微专业 editorial 卡片 → 服务端分页。
 *
 * 数据规则：
 *  · 列表来自 `GET /api/app/micro-major/published`（服务端分页 + 关键词/方向检索），
 *    不再「拉全量后前端 slice」；
 *  · 报名状态来自 `GET /api/app/micro-major/my-enrollments`（报名 / 结业 / 发证三态分开）；
 *  · 卡片上的课程序列来自 `GET /api/app/micro-major/{id}/detail`（仅当前页的微专业）；
 *  · 筛选条件（关键词 q / 方向 dir / 页码 page）全部写进 URL，刷新不丢。
 *
 * 关于「方向」：后端 `PagedMicroMajorRequestDto` 只有一个 `Filter`（对 Title / Summary /
 * IndustryField 做 Contains），没有独立的行业方向入参，也没有方向字典接口。
 * 因此方向筛选复用同一个服务端检索槽位（与关键词互斥，见 applyFilters 注释），
 * 方向候选项来自一次「只取字段不渲染」的已发布列表扫描。
 */
@Component({
  selector: 'app-student-micro-majors',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
  ],
  templateUrl: './student-micro-majors.component.html',
  styleUrls: ['./student-micro-majors.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMicroMajorsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  /** 统一使用生成的代理服务（proxy/micro-majors），不再使用手写的 micro-major.service */
  private readonly microMajorService = inject(MicroMajorService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly items = signal<MicroMajorDto[]>([]);
  readonly total = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(9);

  /** 关键词（写入 URL 的 q） */
  readonly keyword = signal('');
  /** 行业方向（写入 URL 的 dir） */
  readonly direction = signal('');
  /** 方向候选项（真实已发布微专业的 industryField 去重） */
  readonly directions = signal<string[]>([]);

  /** 我的报名（用于卡片上的报名状态徽章） */
  readonly myEnrollments = signal<MicroMajorEnrollmentDto[]>([]);
  /** 当前页各微专业的课程（detail 接口，按 id 索引） */
  readonly courseMap = signal<Record<string, MicroMajorCourseDto[]>>({});
  /** 正在报名中的微专业 id */
  readonly enrollingId = signal<string | null>(null);

  /** 当前是否处于「筛选中」——决定空状态用 result 还是 data */
  readonly hasFilter = computed(() => !!this.keyword().trim() || !!this.direction());

  /** 四项统计：微专业总数 / 已报名 / 已结业 / 已发证（三个状态互不合并） */
  readonly stats = computed<CenterStat[]>(() => {
    const mine = this.myEnrollments();
    const applied = mine.filter(
      e =>
        e.status === MicroMajorEnrollmentStatus.Pending ||
        e.status === MicroMajorEnrollmentStatus.Enrolled ||
        e.status === MicroMajorEnrollmentStatus.InProgress,
    ).length;
    const completed = mine.filter(e => e.status === MicroMajorEnrollmentStatus.Completed).length;
    const certified = mine.filter(e => e.status === MicroMajorEnrollmentStatus.Certified).length;
    return [
      { label: '可报名微专业', value: this.total(), hint: '本校已发布的微专业' },
      { label: '已报名', value: applied, hint: '含待审核与学习中' },
      { label: '已结业', value: completed, hint: '教师已确认结业' },
      { label: '已发证', value: certified, hint: '已发放结业证书' },
    ];
  });

  ngOnInit(): void {
    // URL 是筛选与分页的唯一来源：参数变化 → 重新拉数据
    this.route.queryParams.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.keyword.set((params['q'] as string) ?? '');
      this.direction.set((params['dir'] as string) ?? '');
      const page = Number(params['page']);
      const size = Number(params['size']);
      this.pageIndex.set(Number.isFinite(page) && page > 0 ? page : 1);
      this.pageSize.set(Number.isFinite(size) && size > 0 ? size : 9);
      this.load();
    });

    this.loadMyEnrollments();
    this.loadDirections();
  }

  /** 我的报名列表（报名状态徽章 + 顶部统计） */
  private loadMyEnrollments(): void {
    this.microMajorService.getMyEnrollments().subscribe({
      next: list => this.myEnrollments.set(list || []),
      error: () => this.myEnrollments.set([]),
    });
  }

  /**
   * 方向候选项：扫描一次已发布微专业，只取 industryField 字段。
   *
   * 这不是列表数据源——列表仍然走服务端分页（见 load）。
   * 后端没有方向字典接口，只能从已发布微专业里收集真实出现过的方向。
   */
  private loadDirections(): void {
    this.microMajorService
      .getPublished({ skipCount: 0, maxResultCount: 200 })
      .subscribe({
        next: result => {
          const set = new Set<string>();
          for (const item of result?.items || []) {
            const field = (item.industryField || '').trim();
            if (field) set.add(field);
          }
          this.directions.set(Array.from(set).sort((a, b) => a.localeCompare(b, 'zh-Hans-CN')));
        },
        error: () => this.directions.set([]),
      });
  }

  /** 拉取当前页数据（服务端分页） */
  private load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    const skipCount = (this.pageIndex() - 1) * this.pageSize();
    this.microMajorService
      .getPublished({
        filter: this.serverFilter(),
        skipCount,
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: result => {
          this.items.set(result?.items || []);
          this.total.set(result?.totalCount ?? 0);
          this.loading.set(false);
          this.loadCardCourses(result?.items || []);
        },
        error: () => {
          this.loadFailed.set(true);
          this.loading.set(false);
          this.courseMap.set({});
        },
      });
  }

  /**
   * 送往后端的检索词。
   *
   * 后端只有一个 Filter 槽位（Title / Summary / IndustryField 的 Contains），
   * 无法同时表达「关键词 AND 方向」，因此关键词与方向互斥：
   * 选择方向会清空关键词，反之亦然。
   */
  private serverFilter(): string | undefined {
    const q = this.keyword().trim();
    if (q) return q;
    const dir = this.direction().trim();
    return dir ? dir : undefined;
  }

  /** 卡片上的课程序列：拉取当前页每个微专业的 detail */
  private loadCardCourses(items: MicroMajorDto[]): void {
    const targets = items.filter(m => !!m.id);
    if (targets.length === 0) {
      this.courseMap.set({});
      return;
    }
    forkJoin(
      targets.map(m =>
        this.microMajorService.getDetail(m.id!).pipe(catchError(() => of(null)))
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

  /** 重新加载（错误态重试） */
  reload(): void {
    this.load();
  }

  /** 关键词输入（仅改本地值，提交时才写 URL） */
  setKeyword(value: string): void {
    this.keyword.set(value);
  }

  /** 选择方向（与关键词互斥：后端只有一个检索槽位） */
  selectDirection(value: string): void {
    const next = this.direction() === value ? '' : value;
    this.direction.set(next);
    this.keyword.set('');
    this.applyFilters();
  }

  /** 提交筛选 → 写回 URL（页码重置为 1） */
  applyFilters(event?: Event): void {
    event?.preventDefault();
    const dir = this.direction().trim();
    if (dir) this.keyword.set('');
    this.navigate({
      q: this.keyword().trim() || null,
      dir: dir || null,
      page: null,
    });
  }

  /** 重置筛选 */
  resetFilters(): void {
    this.keyword.set('');
    this.direction.set('');
    this.navigate({ q: null, dir: null, page: null });
  }

  /** 分页变化 */
  onPage(event: { pageIndex: number; pageSize: number }): void {
    this.navigate({ page: event.pageIndex, size: event.pageSize });
  }

  /** 写入 URL（null 表示删除该参数） */
  private navigate(params: Record<string, string | number | null>): void {
    this.router
      .navigate([], {
        relativeTo: this.route,
        queryParams: params,
        queryParamsHandling: 'merge',
        replaceUrl: true,
      })
      .catch(() => undefined);
  }

  /** 该微专业当前是否已报名（含待审核） */
  isApplied(item: MicroMajorDto): boolean {
    return !!item.isCurrentUserEnrolled;
  }

  /** 卡片上的报名状态文案（未报名返回空串） */
  statusLabel(item: MicroMajorDto): string {
    const status = this.enrollmentStatus(item);
    return status === null ? '' : microMajorStatusLabel(status);
  }

  /** 卡片上的报名状态色调 */
  statusTone(item: MicroMajorDto): string {
    const status = this.enrollmentStatus(item);
    if (status === null) return 'default';
    return MICRO_MAJOR_ENROLLMENT_LABELS[status]?.tone ?? 'default';
  }

  /** 该微专业对应的报名状态（我的报名列表里查） */
  enrollmentStatus(item: MicroMajorDto): MicroMajorEnrollmentStatus | null {
    if (!item.id) return null;
    const hit = this.myEnrollments().find(e => e.microMajorId === item.id);
    if (!hit || hit.status === undefined || hit.status === null) return null;
    if (hit.status === MicroMajorEnrollmentStatus.Cancelled) return null;
    return hit.status;
  }

  /** 卡片上的课程列表 */
  cardCourses(item: MicroMajorDto): MicroMajorCourseDto[] {
    return (item.id && this.courseMap()[item.id]) || [];
  }

  /** 卡片上最多展示 3 门课程 */
  cardPreviewCourses(item: MicroMajorDto): MicroMajorCourseDto[] {
    return this.cardCourses(item).slice(0, 3);
  }

  /** 课程总数：优先后端 courseCount，其次已加载的课程数 */
  courseCount(item: MicroMajorDto): number {
    return item.courseCount || this.cardCourses(item).length || 0;
  }

  /** 摘要文案 */
  summaryText(item: MicroMajorDto): string {
    return (item.summary || '').trim() || '系统化课程体系，理论与实践并重。';
  }

  /** 合作单位 */
  collaborationText(item: MicroMajorDto): string {
    return (item.collaborationUnit || '').trim() || '本校建设';
  }

  /** 封面色调序号（4 种） */
  coverTone(item: MicroMajorDto, index: number): number {
    const key = item.title || item.id || `${index}`;
    let sum = 0;
    for (let i = 0; i < key.length; i++) sum = (sum + key.charCodeAt(i)) % 4;
    return sum;
  }

  /** 是否有封面图 */
  hasCover(item: MicroMajorDto): boolean {
    return !!item.coverImageUrl && item.coverImageUrl.trim().length > 0;
  }

  /** 进入详情 */
  openDetail(item: MicroMajorDto): void {
    if (!item.id) return;
    this.router.navigate(['/student/micro-majors', item.id]);
  }

  /** 卡片内的课程链接：透传 fromMicroMajor，课程页可返回微专业 */
  openCourse(item: MicroMajorDto, course: MicroMajorCourseDto, event: Event): void {
    event.stopPropagation();
    if (!course.courseId) return;
    this.router.navigate(['/student/courses', course.courseId], {
      queryParams: { fromMicroMajor: item.id, from: 'micro-majors' },
    });
  }

  /** 提交报名（后端 EnrollAsync 无请求体，状态由后端置为「待审核」） */
  enroll(item: MicroMajorDto, event: Event): void {
    event.stopPropagation();
    if (!item.id || this.enrollingId()) return;
    this.enrollingId.set(item.id);
    this.microMajorService.enroll(item.id).subscribe({
      next: () => {
        this.enrollingId.set(null);
        this.message.success('报名已提交，等待教师审核');
        this.loadMyEnrollments();
        this.load();
      },
      error: (err: any) => {
        this.enrollingId.set(null);
        const msg = err?.error?.error?.message || err?.error?.message;
        this.message.error(msg || '报名失败');
      },
    });
  }
}
