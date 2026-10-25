import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { EmploymentService } from '../../proxy/employment/employment.service';
import { EmploymentApplicationStatus } from '../../proxy/employment/enums';
import type { InterviewScheduleDto, JobApplicationDto } from '../../proxy/employment/dtos/models';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import {
  APPLICATION_STATUS_OPTIONS,
  applicationStage,
  applicationStatusLabel,
  applicationStatusTone,
} from './employment-labels';

/**
 * 学生端 · 我的投递（对齐原型 `#career` 的「我的投递」表格 + `#employment-followup` 的面试安排）。
 *
 * 数据来源（全部真实接口）：
 *   · 投递列表 GET /api/app/employment/my-application-list（服务端分页）
 *   · 面试安排 GET /api/app/employment/interview-list（学生仅可见自己的面试）
 *
 * 「撤回投递」：后端的投递状态更新接口（PUT /api/app/employment/{id}/application-status）
 * 只开放给企业发布者与就业老师（需要 PublishJob + ManageApplication 权限），
 * 学生端没有自助撤回能力，本页如实说明，不做假按钮。
 */
@Component({
  selector: 'app-student-my-applications',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
  ],
  templateUrl: './student-my-applications.component.html',
  styleUrls: ['./student-my-applications.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyApplicationsComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly items = signal<JobApplicationDto[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  /** 状态筛选（与 URL 同步） */
  readonly statusFilter = signal<EmploymentApplicationStatus | null>(null);

  /** 我的面试安排（applicationId → 面试记录） */
  readonly interviewMap = signal<Record<string, InterviewScheduleDto>>({});
  /** 展开详情的投递 id */
  readonly expandedKey = signal<string | null>(null);

  readonly statusOptions = APPLICATION_STATUS_OPTIONS;

  /** 是否存在筛选条件 */
  readonly hasFilters = computed(() => this.statusFilter() !== null);

  /** 页头数据总览（数字来自真实接口） */
  readonly stats = computed(() => {
    const list = this.items();
    return [
      { label: '累计投递', value: this.totalCount(), hint: '全部投递记录总数' },
      {
        label: '本页面试中',
        value: list.filter(a => applicationStage(a.status) === 2).length,
        hint: '当前页处于面试阶段',
      },
      {
        label: '本页已录用',
        value: list.filter(a => applicationStage(a.status) === 4).length,
        hint: '当前页已收到录用',
      },
    ];
  });

  ngOnInit(): void {
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.applyQuery(params);
        this.loadItems();
      });
  }

  private applyQuery(params: ParamMap): void {
    const status = params.get('status');
    this.statusFilter.set(
      status !== null && status !== '' ? (Number(status) as EmploymentApplicationStatus) : null,
    );

    const page = Number(params.get('page') || '1');
    this.pageIndex.set(Number.isFinite(page) && page > 0 ? Math.floor(page) : 1);
  }

  private navigate(params: Record<string, string | null>, replaceUrl = true): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: params,
      queryParamsHandling: 'merge',
      replaceUrl,
    });
  }

  /** 加载投递列表（服务端分页） */
  private loadItems(): void {
    this.loading.set(true);
    this.loadFailed.set(false);

    this.employmentService
      .getMyApplicationList({
        status: this.statusFilter() ?? undefined,
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: result => {
          const items = result?.items || [];
          this.items.set(items);
          this.totalCount.set(result?.totalCount || 0);
          this.loading.set(false);
          if (items.some(a => applicationStage(a.status) >= 2)) this.loadInterviews();
        },
        error: () => {
          this.items.set([]);
          this.loading.set(false);
          this.loadFailed.set(true);
        },
      });
  }

  /** 加载我的面试安排（学生仅可见自己的面试记录） */
  private loadInterviews(): void {
    this.employmentService
      .getInterviewList({ skipCount: 0, maxResultCount: 100 })
      .subscribe({
        next: result => {
          const map: Record<string, InterviewScheduleDto> = {};
          for (const iv of result?.items || []) {
            if (!iv.applicationId) continue;
            const current = map[iv.applicationId];
            if (
              !current ||
              new Date(iv.creationTime || 0).getTime() > new Date(current.creationTime || 0).getTime()
            ) {
              map[iv.applicationId] = iv;
            }
          }
          this.interviewMap.set(map);
        },
        error: () => undefined,
      });
  }

  /** 重新加载 */
  reload(): void {
    this.loadItems();
  }

  /** 选择状态筛选 */
  setStatus(status: EmploymentApplicationStatus | null): void {
    this.statusFilter.set(status);
    this.navigate({ status: status === null ? null : String(status), page: null });
  }

  /** 分页 */
  onPageChange(event: { pageIndex: number; pageSize: number }): void {
    this.pageIndex.set(event.pageIndex);
    this.navigate({ page: String(event.pageIndex) }, false);
  }

  /** 展开 / 收起详情 */
  toggleExpand(id: string): void {
    this.expandedKey.set(this.expandedKey() === id ? null : id);
  }

  /** 状态文案 */
  statusLabel(item: JobApplicationDto): string {
    return applicationStatusLabel(item.status);
  }

  /** 状态色调 */
  statusTone(item: JobApplicationDto): string {
    return applicationStatusTone(item.status);
  }

  /** 进度阶段 */
  stage(item: JobApplicationDto): number {
    return applicationStage(item.status);
  }

  /** 去岗位详情 */
  goJob(item: JobApplicationDto): void {
    if (!item.jobPostingId) return;
    if (!item.jobTitle) {
      // 岗位被删除时后端解析不出标题，直接提示，不再发请求
      return;
    }
    this.router.navigate(['/student/employment/jobs', item.jobPostingId]);
  }

  /** 去就业中心 */
  goJobs(): void {
    this.router.navigate(['/student/employment/jobs']);
  }

  /** 岗位已下线时给出说明 */
  jobTitle(item: JobApplicationDto): string {
    return item.jobTitle || '岗位已下线或被删除';
  }
}
