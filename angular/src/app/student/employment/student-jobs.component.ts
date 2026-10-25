import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { EmploymentService } from '../../proxy/employment/employment.service';
import { EmploymentJobType } from '../../proxy/employment/enums';
import type { JobPostingDto } from '../../proxy/employment/dtos/models';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import {
  JOB_TYPE_OPTIONS,
  applicationStatusLabel,
  applicationStatusTone,
  jobTypeLabel,
  splitTagList,
} from './employment-labels';

/**
 * 学生端 · 就业中心（对齐原型 `#jobs`）。
 *
 * 结构：页头 → 关键词 / 类型 / 地点筛选 → 岗位卡列表 → 服务端分页。
 *
 * 数据来源（全部真实接口）：
 *   · 岗位列表 GET /api/app/employment/published-job-list（服务端分页）
 *
 * 筛选条件与页码全部写进 URL query params（刷新不丢，前进后退可回显）。
 */
@Component({
  selector: 'app-student-jobs',
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
  templateUrl: './student-jobs.component.html',
  styleUrls: ['./student-jobs.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentJobsComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  /** 岗位列表 */
  readonly jobs = signal<JobPostingDto[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(12);

  /** 与 URL 同步的筛选状态 */
  readonly keyword = signal('');
  readonly location = signal('');
  readonly jobType = signal<EmploymentJobType | null>(null);

  /** 岗位类型筛选项 */
  readonly jobTypeOptions = JOB_TYPE_OPTIONS;

  /** 是否存在筛选条件（决定空态语义与「重置筛选」是否可用） */
  readonly hasFilters = computed(
    () => !!this.keyword().trim() || !!this.location().trim() || this.jobType() !== null,
  );

  /** 页头数据总览（数字全部来自真实接口返回） */
  readonly stats = computed(() => {
    const list = this.jobs();
    const applied = list.filter(j => j.hasApplied).length;
    return [
      { label: '在招岗位', value: this.totalCount(), hint: '当前筛选条件下的岗位总数' },
      { label: '本页已投递', value: applied, hint: '当前页已投递的岗位数' },
      { label: '本页待投递', value: list.length - applied, hint: '当前页可投递的岗位数' },
    ];
  });

  ngOnInit(): void {
    // 筛选 / 页码都在 URL 上：订阅 queryParamMap，浏览器前进后退也能正确回显。
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.applyQuery(params);
        this.loadJobs();
      });
  }

  /** 把 URL 上的查询参数同步到信号 */
  private applyQuery(params: ParamMap): void {
    // 兼容旧链接的 keyword 参数（岗位详情页「查看该公司其他岗位」历史上用它）
    const keyword = params.get('q') ?? params.get('keyword') ?? '';
    this.keyword.set((keyword || '').trim());

    this.location.set((params.get('location') || '').trim());

    const type = params.get('type');
    this.jobType.set(type !== null && type !== '' ? (Number(type) as EmploymentJobType) : null);

    const page = Number(params.get('page') || '1');
    this.pageIndex.set(Number.isFinite(page) && page > 0 ? Math.floor(page) : 1);
  }

  /** 写回 URL（null 表示移除该参数） */
  private navigate(params: Record<string, string | null>, replaceUrl = true): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: params,
      queryParamsHandling: 'merge',
      replaceUrl,
    });
  }

  /** 加载岗位列表（服务端分页） */
  private loadJobs(): void {
    this.loading.set(true);
    this.loadFailed.set(false);

    this.employmentService
      .getPublishedJobList({
        filter: this.keyword().trim() || undefined,
        location: this.location().trim() || undefined,
        jobType: this.jobType() ?? undefined,
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: result => {
          this.jobs.set(result?.items || []);
          this.totalCount.set(result?.totalCount || 0);
          this.loading.set(false);
        },
        error: () => {
          this.jobs.set([]);
          this.loading.set(false);
          this.loadFailed.set(true);
        },
      });
  }

  /** 重新加载（错误态重试） */
  reload(): void {
    this.loadJobs();
  }

  /** 提交检索 */
  onSearch(): void {
    this.navigate({ q: this.keyword().trim() || null, keyword: null, page: null });
  }

  /** 选择岗位类型 */
  setJobType(type: EmploymentJobType | null): void {
    this.jobType.set(type);
    this.navigate({ type: type === null ? null : String(type), page: null });
  }

  /** 重置筛选 */
  resetFilters(): void {
    this.keyword.set('');
    this.location.set('');
    this.jobType.set(null);
    this.navigate({ q: null, keyword: null, location: null, type: null, page: null });
  }

  /** 分页 */
  onPageChange(event: { pageIndex: number; pageSize: number }): void {
    this.pageIndex.set(event.pageIndex);
    this.navigate({ page: String(event.pageIndex) }, false);
  }

  /** 去我的简历（空态引导） */
  goResumes(): void {
    this.router.navigate(['/student/employment/my-resumes']);
  }

  /** 岗位状态标签文案 */
  statusLabel(item: JobPostingDto): string {
    return applicationStatusLabel(item.applicationStatus);
  }

  /** 岗位状态标签色调 */
  statusTone(item: JobPostingDto): string {
    return applicationStatusTone(item.applicationStatus);
  }

  /** 岗位类型文案 */
  typeLabel(item: JobPostingDto): string {
    return jobTypeLabel(item.jobType);
  }

  /** 技能标签（最多展示 4 个） */
  skillTags(item: JobPostingDto): string[] {
    return splitTagList(item.skillTags).slice(0, 4);
  }

  /** 截止日期提示 */
  deadlineHint(item: JobPostingDto): string {
    if (!item.deadline) return '长期招聘';
    const end = new Date(item.deadline).getTime();
    if (Number.isNaN(end)) return '长期招聘';
    const days = Math.ceil((end - Date.now()) / 86400000);
    if (days < 0) return '已截止';
    if (days === 0) return '今天截止';
    return `剩 ${days} 天`;
  }

  /** 是否已过截止日期 */
  isExpired(item: JobPostingDto): boolean {
    if (!item.deadline) return false;
    const end = new Date(item.deadline).getTime();
    if (Number.isNaN(end)) return false;
    return end < Date.now();
  }
}
