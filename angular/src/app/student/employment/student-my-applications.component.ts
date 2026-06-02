import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { EmploymentApplicationStatus, EmploymentService, JobApplicationDto } from '../../employment/employment.service';

interface StatItem {
  label: string;
  value: number;
  suffix: string;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-student-my-applications',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterLink,
    NzIconModule,
    NzSpinModule,
    NzEmptyModule,
    NzPaginationModule,
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

  readonly stats = computed<StatItem[]>(() => {
    const items = this.items();
    const total = this.totalCount();
    const submitted = items.filter(x => x.status === EmploymentApplicationStatus.Submitted).length;
    const interview = items.filter(x => x.status === EmploymentApplicationStatus.InterviewScheduled).length;
    const offered = items.filter(x => x.status === EmploymentApplicationStatus.Offered).length;
    const rejected = items.filter(x => x.status === EmploymentApplicationStatus.Rejected).length;
    return [
      { label: '累计投递', value: total, suffix: '份', icon: 'paper-plane', color: 'linear-gradient(135deg, #1e6ce8 0%, #00b7ff 100%)' },
      { label: '已投递', value: submitted, suffix: '份', icon: 'check-circle', color: 'linear-gradient(135deg, #0c4cb8 0%, #1e6ce8 100%)' },
      { label: '面试邀请', value: interview, suffix: '份', icon: 'calendar', color: 'linear-gradient(135deg, #00b7ff 0%, #38bdf8 100%)' },
      { label: '已录用', value: offered, suffix: '份', icon: 'trophy', color: 'linear-gradient(135deg, #10b981 0%, #06b6d4 100%)' },
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
        this.items.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载投递记录失败');
      },
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

  openJobDetail(item: JobApplicationDto): void {
    this.router.navigate(['/student/employment/jobs', item.jobPostingId]);
  }

  getStatusInfo(status: EmploymentApplicationStatus): { label: string; color: string; icon: string } {
    const map: Record<number, { label: string; color: string; icon: string }> = {
      [EmploymentApplicationStatus.Submitted]: { label: '已投递', color: '#1e6ce8', icon: 'paper-plane' },
      [EmploymentApplicationStatus.Viewed]: { label: '已查看', color: '#6366f1', icon: 'eye' },
      [EmploymentApplicationStatus.InterviewScheduled]: { label: '面试邀请', color: '#00b7ff', icon: 'calendar' },
      [EmploymentApplicationStatus.Offered]: { label: '已录用', color: '#10b981', icon: 'trophy' },
      [EmploymentApplicationStatus.Rejected]: { label: '未通过', color: '#ef4444', icon: 'close-circle' },
      [EmploymentApplicationStatus.Withdrawn]: { label: '已撤回', color: '#94a3b8', icon: 'rollback' },
    };
    return map[status] || { label: '未知', color: '#94a3b8', icon: 'question' };
  }

  /** 状态进度条 (0~3) */
  getStatusStep(status: EmploymentApplicationStatus): { current: number; total: number } {
    const order: Record<number, number> = {
      [EmploymentApplicationStatus.Submitted]: 1,
      [EmploymentApplicationStatus.Viewed]: 2,
      [EmploymentApplicationStatus.InterviewScheduled]: 2,
      [EmploymentApplicationStatus.Offered]: 3,
      [EmploymentApplicationStatus.Rejected]: 3,
      [EmploymentApplicationStatus.Withdrawn]: 0,
    };
    return { current: order[status] ?? 0, total: 3 };
  }

  coverGradient(item: JobApplicationDto): string {
    const palettes = [
      'linear-gradient(135deg, #1e6ce8 0%, #00b7ff 100%)',
      'linear-gradient(135deg, #0c4cb8 0%, #1e6ce8 60%, #00b7ff 100%)',
      'linear-gradient(135deg, #1d4ed8 0%, #38bdf8 100%)',
      'linear-gradient(135deg, #2563eb 0%, #06b6d4 100%)',
    ];
    const key = item.id || item.jobTitle || '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
    return palettes[Math.abs(hash) % palettes.length];
  }
}
