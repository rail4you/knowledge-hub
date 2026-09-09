import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  EmploymentApplicationStatus,
  EmploymentJobType,
  EmploymentService,
  InterviewScheduleDto,
  JobPostingDto,
} from '../../employment/employment.service';

/**
 * 学生端岗位详情弹窗：在 /student/employment/jobs 列表页点击卡片时弹出，
 * 不做路由跳转。路由 /student/employment/jobs/:id 保留（深链/复制链接可用）。
 */
@Component({
  selector: 'app-student-job-detail-modal',
  standalone: true,
  imports: [CommonModule, DatePipe, NzIconModule, NzSpinModule, NzModalModule, NzTagModule],
  templateUrl: './student-job-detail-modal.component.html',
  styleUrls: ['./student-job-detail-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentJobDetailModalComponent {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  /** 为 null 时弹窗关闭；有值时加载对应岗位 */
  readonly jobId = input<string | null>(null);
  readonly closed = output<void>();

  readonly job = signal<JobPostingDto | null>(null);
  readonly loading = signal(false);
  readonly interview = signal<InterviewScheduleDto | null>(null);

  readonly jobTypes = EmploymentJobType;
  readonly appStatus = EmploymentApplicationStatus;

  readonly modalTitle = computed(() => this.job()?.title || '岗位详情');

  constructor() {
    effect(() => {
      const id = this.jobId();
      if (!id) return;
      this.loadJob(id);
    });
  }

  onCancel(): void {
    this.closed.emit();
  }

  private loadJob(id: string): void {
    this.loading.set(true);
    this.job.set(null);
    this.interview.set(null);
    this.employmentService.getJob(id).subscribe({
      next: j => {
        this.job.set(j);
        this.loading.set(false);
        if (j.hasApplied) this.loadInterview(j.id);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载岗位失败');
        this.closed.emit();
      },
    });
  }

  private loadInterview(jobId: string): void {
    this.employmentService.getInterviewList({ skipCount: 0, maxResultCount: 50 }).subscribe({
      next: result => {
        const list = (result.items || []).filter(x => x.jobPostingId === jobId);
        if (list.length > 0) {
          list.sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime());
          this.interview.set(list[0]);
        }
      },
    });
  }

  // ---- 显示辅助 ----
  getTypeLabel(t: EmploymentJobType): string {
    const m: Record<number, string> = { [EmploymentJobType.FullTime]: '全职', [EmploymentJobType.Internship]: '实习', [EmploymentJobType.PartTime]: '兼职', [EmploymentJobType.Apprenticeship]: '学徒' };
    return m[t] || '其他';
  }

  getTypeColor(t: EmploymentJobType): string {
    const m: Record<number, string> = { [EmploymentJobType.FullTime]: '#2b6cd4', [EmploymentJobType.Internship]: '#5b93db', [EmploymentJobType.PartTime]: '#10b981', [EmploymentJobType.Apprenticeship]: '#f59e0b' };
    return m[t] || '#6b7280';
  }

  getStatusLabel(s: EmploymentApplicationStatus): string {
    const m: Record<number, string> = {
      [EmploymentApplicationStatus.Submitted]: '已投递', [EmploymentApplicationStatus.Viewed]: '已查看',
      [EmploymentApplicationStatus.InterviewScheduled]: '等待面试', [EmploymentApplicationStatus.InterviewCompleted]: '面试完成',
      [EmploymentApplicationStatus.Offered]: '已录用', [EmploymentApplicationStatus.Rejected]: '未通过', [EmploymentApplicationStatus.Withdrawn]: '已撤回',
    };
    return m[s] ?? '已投递';
  }

  getStatusColor(s: EmploymentApplicationStatus): string {
    const m: Record<number, string> = {
      [EmploymentApplicationStatus.Submitted]: '#2b6cd4', [EmploymentApplicationStatus.Viewed]: '#6366f1',
      [EmploymentApplicationStatus.InterviewScheduled]: '#0891b2', [EmploymentApplicationStatus.InterviewCompleted]: '#7c3aed',
      [EmploymentApplicationStatus.Offered]: '#10b981', [EmploymentApplicationStatus.Rejected]: '#ef4444', [EmploymentApplicationStatus.Withdrawn]: '#94a3b8',
    };
    return m[s] ?? '#6b7280';
  }

  /** 描述文本裁掉首尾空白：数据里常带多余换行，pre-wrap 会原样撑出大片空白 */
  getDescription(item: JobPostingDto): string {
    return (item.description || '').trim().replace(/\n{3,}/g, '\n\n');
  }

  getSkillTags(item: JobPostingDto): string[] {
    if (!item.skillTags) return [];
    return item.skillTags.split(/[,,;;\n]/).map(s => s.trim()).filter(Boolean);
  }

  getBenefitItems(item: JobPostingDto): string[] {
    if (!item.benefits) return [];
    return item.benefits.split(/[,,;;\n]/).map(s => s.trim()).filter(Boolean);
  }

  deadlineDays(item: JobPostingDto): number | null {
    if (!item.deadline) return null;
    const end = new Date(item.deadline).getTime();
    if (Number.isNaN(end)) return null;
    return Math.ceil((end - Date.now()) / 86400000);
  }

  deadlineHint(item: JobPostingDto): string {
    const d = this.deadlineDays(item);
    if (d === null) return '长期有效';
    if (d < 0) return '已截止';
    if (d === 0) return '今天截止';
    return `剩 ${d} 天`;
  }

  isExpired(item: JobPostingDto): boolean {
    const d = this.deadlineDays(item);
    return d !== null && d < 0;
  }

  stage(): number {
    const s = this.job()?.applicationStatus;
    if (s == null) return 0;
    const m: Record<number, number> = {
      [EmploymentApplicationStatus.Submitted]: 1, [EmploymentApplicationStatus.Viewed]: 1,
      [EmploymentApplicationStatus.InterviewScheduled]: 2, [EmploymentApplicationStatus.InterviewCompleted]: 3,
      [EmploymentApplicationStatus.Offered]: 4, [EmploymentApplicationStatus.Rejected]: -1, [EmploymentApplicationStatus.Withdrawn]: 0,
    };
    return m[s] ?? 0;
  }
}
