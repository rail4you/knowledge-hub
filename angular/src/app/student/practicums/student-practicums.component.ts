import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import { LearningService } from '../../proxy/learning/learning.service';
import type { PracticumProjectDto } from '../../proxy/practicums/dtos/models';
import type { StudentCourseListItemDto } from '../../proxy/learning/dtos/models';
import { PracticumEnrollmentStatus } from '../../proxy/practicums/enums/practicum-enrollment-status.enum';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';

/** 顶部统计卡 */
interface CenterStat {
  label: string;
  value: number;
  hint: string;
}

/** 筛选用的课程选项 */
interface CourseOption {
  id: string;
  name: string;
}

/**
 * 学生端 · 实训中心（原型 #training）。
 *
 * 结构：Hero → 四项统计 → 筛选 → 实训项目卡（3 列）→ 服务端分页。
 *
 * 数据规则：
 *  · 项目列表来自 `GET /api/app/practicum/published`（服务端分页 + 关键词过滤），
 *    不再拉全量后前端 slice；
 *  · 筛选条件（关键词 / 关联课程 / 页码）全部写进 URL，刷新不丢；
 *  · 我的实训统计来自 `GET /api/app/practicum/my-enrollments`。
 */
@Component({
  selector: 'app-student-practicums',
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
  templateUrl: './student-practicums.component.html',
  styleUrls: ['./student-practicums.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentPracticumsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly practicumService = inject(PracticumService);
  private readonly learningService = inject(LearningService);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly items = signal<PracticumProjectDto[]>([]);
  readonly total = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(9);

  /** 关键词（写入 URL 的 q） */
  readonly keyword = signal('');
  /** 关联课程（写入 URL 的 course） */
  readonly courseId = signal('');
  /** 我的实训（用于顶部统计） */
  readonly myEnrollments = signal<{ status?: PracticumEnrollmentStatus }[]>([]);
  /** 课程下拉选项（来自真实课程接口） */
  readonly courses = signal<CourseOption[]>([]);

  /** 当前是否处于「筛选中」——决定空状态用 result 还是 data */
  readonly hasFilter = computed(() => !!this.keyword().trim() || !!this.courseId());

  /** 四项统计（全部来自真实接口） */
  readonly stats = computed<CenterStat[]>(() => {
    const mine = this.myEnrollments();
    const pending = mine.filter(e => e.status === PracticumEnrollmentStatus.Submitted).length;
    const doing = mine.filter(
      e =>
        e.status === PracticumEnrollmentStatus.Enrolled ||
        e.status === PracticumEnrollmentStatus.InProgress ||
        e.status === PracticumEnrollmentStatus.Reviewed,
    ).length;
    return [
      {
        label: '实训项目',
        value: this.total(),
        hint: this.hasFilter() ? '按当前筛选条件统计' : '已发布的实训项目',
      },
      { label: '我的实训', value: mine.length, hint: '我参与过的项目' },
      { label: '待完成', value: doing, hint: '未提交或推进中的项目' },
      { label: '待批阅', value: pending, hint: '已提交，等待教师批阅' },
    ];
  });

  ngOnInit(): void {
    // URL 是筛选与分页的唯一来源：参数变化 → 重新拉数据
    this.route.queryParams.pipe(takeUntilDestroyed()).subscribe(params => {
      this.keyword.set((params['q'] as string) ?? '');
      this.courseId.set((params['course'] as string) ?? '');
      const page = Number(params['page']);
      const size = Number(params['size']);
      this.pageIndex.set(Number.isFinite(page) && page > 0 ? page : 1);
      this.pageSize.set(Number.isFinite(size) && size > 0 ? size : 9);
      this.load();
    });

    this.loadCourses();
    this.loadMyEnrollments();
  }

  /** 我的实训（顶部统计用） */
  private loadMyEnrollments(): void {
    this.practicumService.getMyEnrollments().subscribe({
      next: list => this.myEnrollments.set(list || []),
      error: () => this.myEnrollments.set([]),
    });
  }

  /** 课程下拉选项（真实接口；失败时隐藏该筛选项，不影响主流程） */
  private loadCourses(): void {
    this.learningService.getMyCourses().subscribe({
      next: list => {
        const options: CourseOption[] = (list || [])
          .map((c: StudentCourseListItemDto) => ({ id: c.courseId || '', name: c.courseTitle || '' }))
          .filter(c => !!c.id && !!c.name);
        this.courses.set(options);
      },
      error: () => this.courses.set([]),
    });
  }

  /** 拉取当前页数据（服务端分页） */
  private load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    const skipCount = (this.pageIndex() - 1) * this.pageSize();
    this.practicumService
      .getPublished({
        filter: this.keyword().trim() || undefined,
        courseId: this.courseId() || undefined,
        skipCount,
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: result => {
          this.items.set(result?.items || []);
          this.total.set(result?.totalCount ?? 0);
          this.loading.set(false);
        },
        error: () => {
          this.loadFailed.set(true);
          this.loading.set(false);
        },
      });
  }

  /** 重新加载（错误态重试） */
  reload(): void {
    this.load();
  }

  /** 关键词输入（仅改本地值，回车/点搜索才写 URL） */
  setKeyword(value: string): void {
    this.keyword.set(value);
  }

  /** 切换关联课程筛选 */
  setCourse(value: string): void {
    this.courseId.set(value);
    this.applyFilters();
  }

  /** 提交筛选 → 写回 URL（页码重置为 1） */
  applyFilters(event?: Event): void {
    event?.preventDefault();
    this.navigate({ q: this.keyword().trim() || null, course: this.courseId() || null, page: null });
  }

  /** 重置筛选 */
  resetFilters(): void {
    this.keyword.set('');
    this.courseId.set('');
    this.navigate({ q: null, course: null, page: null });
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

  /** 卡片状态标签：开放报名 / 已报名 / 已结束 */
  statusText(item: PracticumProjectDto): string {
    if (item.isExpired) return '已结束';
    if (item.isCurrentUserEnrolled) return '已报名';
    return '开放报名';
  }

  /** 卡片状态色调 */
  statusTone(item: PracticumProjectDto): 'ok' | 'gray' | 'default' {
    if (item.isExpired) return 'gray';
    if (item.isCurrentUserEnrolled) return 'ok';
    return 'default';
  }

  /** 封面色调序号（4 种，与原型一致） */
  coverTone(index: number): number {
    return index % 4;
  }

  /** 前往仿真实训中心 */
  navigateToWasm(): void {
    this.router.navigate(['/student/wasm-center']);
  }
}
