import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  EmploymentApplicationStatus,
  EmploymentInterviewResult,
  EmploymentService,
  InterviewScheduleDto,
  JobApplicationDto,
} from '../../employment/employment.service';

@Component({
  selector: 'app-student-my-applications',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, FormsModule, RouterLink,
    NzIconModule, NzSpinModule, NzEmptyModule, NzPaginationModule, NzTagModule,
  ],
  templateUrl: './student-my-applications.component.html',
  styleUrls: ['./student-my-applications.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyApplicationsComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly items = signal<JobApplicationDto[]>([]);
  readonly loading = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly statusFilter = signal<EmploymentApplicationStatus | null>(null);
  readonly statuses = EmploymentApplicationStatus;

  /** applicationId → 该投递关联的最新面试记录 */
  readonly interviewMap = signal<Record<string, InterviewScheduleDto>>({});

  readonly stats = computed(() => {
    const items = this.items();
    const total = this.totalCount();
    const interview = items.filter(x =>
      x.status === EmploymentApplicationStatus.InterviewScheduled ||
      x.status === EmploymentApplicationStatus.InterviewCompleted
    ).length;
    const offered = items.filter(x => x.status === EmploymentApplicationStatus.Offered).length;
    return [
      { label: '累计投递', value: total, suffix: '份', color: '#1e6ce8', icon: 'paper-plane' },
      { label: '面试中', value: interview, suffix: '份', color: '#0891b2', icon: 'calendar' },
      { label: '已录用', value: offered, suffix: '份', color: '#10b981', icon: 'trophy' },
    ];
  });

  ngOnInit(): void {
    this.loadItems();
  }

  loadItems(): void {
    this.loading.set(true);
    this.employmentService.getMyApplicationList({
      status: this.statusFilter() ?? undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    }).subscribe({
      next: result => {
        const apps = result.items || [];
        this.items.set(apps);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
        // 加载关联的面试记录
        if (apps.some(a =>
          a.status === EmploymentApplicationStatus.InterviewScheduled ||
          a.status === EmploymentApplicationStatus.InterviewCompleted
        )) {
          this.loadInterviews();
        }
      },
      error: () => { this.loading.set(false); this.message.error('加载投递记录失败'); },
    });
  }

  private loadInterviews(): void {
    this.employmentService.getInterviewList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: result => {
        const map: Record<string, InterviewScheduleDto> = {};
        for (const iv of (result.items || [])) {
          // 每个 application 保留最新的一条面试
          if (!map[iv.applicationId] || new Date(iv.creationTime) > new Date(map[iv.applicationId].creationTime)) {
            map[iv.applicationId] = iv;
          }
        }
        this.interviewMap.set(map);
      },
      error: () => { /* 静默 - 面试数据加载失败不影响主列表 */ },
    });
  }

  onFilterChange(): void {
    this.pageIndex.set(1);
    this.loadItems();
  }

  onPageChange(page: number): void {
    this.pageIndex.set(page);
    this.loadItems();
  }

  /** 跳转到岗位详情 */
  goJob(item: JobApplicationDto): void {
    this.router.navigate(['/student/employment/jobs', item.jobPostingId]);
  }

  getStatusLabel(s: EmploymentApplicationStatus): string {
    const m: Record<number, string> = {
      [EmploymentApplicationStatus.Submitted]: '已投递',
      [EmploymentApplicationStatus.Viewed]: '已查看',
      [EmploymentApplicationStatus.InterviewScheduled]: '等待面试',
      [EmploymentApplicationStatus.InterviewCompleted]: '面试完成',
      [EmploymentApplicationStatus.Offered]: '已录用',
      [EmploymentApplicationStatus.Rejected]: '未通过',
      [EmploymentApplicationStatus.Withdrawn]: '已撤回',
    };
    return m[s] ?? '未知';
  }

  getStatusColor(s: EmploymentApplicationStatus): string {
    const m: Record<number, string> = {
      [EmploymentApplicationStatus.Submitted]: '#1e6ce8',
      [EmploymentApplicationStatus.Viewed]: '#6366f1',
      [EmploymentApplicationStatus.InterviewScheduled]: '#0891b2',
      [EmploymentApplicationStatus.InterviewCompleted]: '#7c3aed',
      [EmploymentApplicationStatus.Offered]: '#10b981',
      [EmploymentApplicationStatus.Rejected]: '#ef4444',
      [EmploymentApplicationStatus.Withdrawn]: '#94a3b8',
    };
    return m[s] ?? '#6b7280';
  }

  getStageIndex(s: EmploymentApplicationStatus): number {
    const order: Record<number, number> = {
      [EmploymentApplicationStatus.Submitted]: 1,
      [EmploymentApplicationStatus.Viewed]: 1,
      [EmploymentApplicationStatus.InterviewScheduled]: 2,
      [EmploymentApplicationStatus.InterviewCompleted]: 3,
      [EmploymentApplicationStatus.Offered]: 4,
      [EmploymentApplicationStatus.Rejected]: -1,
      [EmploymentApplicationStatus.Withdrawn]: 0,
    };
    return order[s] ?? 0;
  }
}
