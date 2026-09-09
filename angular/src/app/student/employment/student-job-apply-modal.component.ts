import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { EmploymentService, JobPostingDto, StudentResumeDto } from '../../employment/employment.service';

/**
 * 学生端独立投递弹窗：从岗位列表点「立即应聘」直接弹出，
 * 只做一件事——选简历 → 填求职说明（可选）→ 确认投递。
 * 与岗位详情弹窗完全独立。
 */
@Component({
  selector: 'app-student-job-apply-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, NzButtonModule, NzIconModule, NzInputModule, NzModalModule, NzSelectModule, NzSpinModule],
  templateUrl: './student-job-apply-modal.component.html',
  styleUrls: ['./student-job-apply-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentJobApplyModalComponent {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  /** 为 null 时弹窗关闭；有值时加载对应岗位并打开投递表单 */
  readonly jobId = input<string | null>(null);
  readonly closed = output<void>();
  /** 投递成功时触发（值为岗位 id），父组件可据此刷新列表 */
  readonly applied = output<string>();

  readonly job = signal<JobPostingDto | null>(null);
  readonly loading = signal(false);
  readonly resumes = signal<StudentResumeDto[]>([]);
  readonly selectedResumeId = signal('');
  readonly coverLetter = signal('');
  readonly submitting = signal(false);

  constructor() {
    effect(() => {
      const id = this.jobId();
      // 每次打开重置表单
      this.coverLetter.set('');
      this.selectedResumeId.set('');
      if (!id) return;
      this.loadJob(id);
      this.loadResumes();
    });
  }

  onCancel(): void {
    this.closed.emit();
  }

  apply(): void {
    const j = this.job();
    if (!j || j.hasApplied || this.submitting()) return;
    if (this.resumes().length === 0) {
      this.message.warning('请先创建简历后再投递');
      return;
    }
    if (!this.selectedResumeId()) {
      this.message.warning('请选择一份简历后再投递');
      return;
    }

    this.submitting.set(true);
    this.employmentService.createApplication({
      jobPostingId: j.id,
      resumeId: this.selectedResumeId(),
      coverLetter: this.coverLetter() || undefined,
    }).subscribe({
      next: () => {
        this.message.success('投递成功！');
        this.submitting.set(false);
        this.applied.emit(j.id);
        this.closed.emit();
      },
      error: () => {
        this.submitting.set(false);
        this.message.error('投递失败');
      },
    });
  }

  private loadJob(id: string): void {
    this.loading.set(true);
    this.job.set(null);
    this.employmentService.getJob(id).subscribe({
      next: j => {
        this.job.set(j);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载岗位失败');
        this.closed.emit();
      },
    });
  }

  private loadResumes(): void {
    this.employmentService.getMyResumeList().subscribe({
      next: items => {
        const list = items || [];
        this.resumes.set(list);
        // 自动默认选中默认简历（没有则取第一份）
        const d = list.find(x => x.isDefault) || list[0];
        this.selectedResumeId.set(d?.id || '');
      },
    });
  }

  isExpired(item: JobPostingDto): boolean {
    if (!item.deadline) return false;
    const end = new Date(item.deadline).getTime();
    if (Number.isNaN(end)) return false;
    return end < Date.now();
  }
}
