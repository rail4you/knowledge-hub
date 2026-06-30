import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import {
  EmploymentApplicationStatus,
  EmploymentJobType,
  EmploymentService,
  InterviewScheduleDto,
  JobPostingDto,
  StudentResumeDto,
} from '../../employment/employment.service';

@Component({
  selector: 'app-student-job-detail',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, RouterLink, NzIconModule, NzSpinModule, NzTagModule, NzProgressModule],
  templateUrl: './student-job-detail.component.html',
  styleUrls: ['./student-job-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentJobDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly job = signal<JobPostingDto | null>(null);
  readonly resumes = signal<StudentResumeDto[]>([]);
  readonly loading = signal(false);
  readonly submitting = signal(false);
  readonly selectedResumeId = signal('');
  readonly coverLetter = signal('');
  readonly jobTypes = EmploymentJobType;
  readonly appStatus = EmploymentApplicationStatus;
  /** 当前岗位关联的面试记录 */
  readonly interview = signal<InterviewScheduleDto | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.message.error('岗位不存在'); this.router.navigate(['/student/employment/jobs']); return; }

    this.loading.set(true);
    this.employmentService.getJob(id).subscribe({
      next: job => {
        this.job.set(job);
        this.loading.set(false);
        // 如果已投递，加载面试记录
        if (job.hasApplied) { this.loadInterview(job.id); }
      },
      error: () => { this.loading.set(false); this.message.error('加载岗位失败'); this.router.navigate(['/student/employment/jobs']); },
    });

    this.employmentService.getMyResumeList().subscribe(items => {
      const list = items || [];
      this.resumes.set(list);
      const d = list.find(x => x.isDefault) || list[0];
      if (d) this.selectedResumeId.set(d.id);
    });
  }

  /** 加载该岗位关联的面试记录 */
  loadInterview(jobId: string): void {
    this.employmentService.getInterviewList({ skipCount: 0, maxResultCount: 50 }).subscribe({
      next: result => {
        const list = (result.items || []).filter(x => x.jobPostingId === jobId);
        // 取最新一条
        if (list.length > 0) {
          list.sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime());
          this.interview.set(list[0]);
        }
      },
    });
  }

  apply(): void {
    const j = this.job();
    if (!j || j.hasApplied) return;
    if (!this.selectedResumeId()) { this.message.warning('请选择一份简历后再投递'); return; }
    if (this.resumes().length === 0) { this.message.warning('请先创建简历后再投递'); return; }

    this.submitting.set(true);
    this.employmentService.createApplication({ jobPostingId: j.id, resumeId: this.selectedResumeId(), coverLetter: this.coverLetter() || undefined }).subscribe({
      next: () => {
        this.message.success('投递成功！');
        this.submitting.set(false);
        this.employmentService.getJob(j.id).subscribe(item => { this.job.set(item); if (item.hasApplied) this.loadInterview(item.id); });
      },
      error: () => { this.submitting.set(false); this.message.error('投递失败'); },
    });
  }

  goMyResumes(): void { this.router.navigate(['/student/employment/my-resumes']); }

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
      [EmploymentApplicationStatus.Submitted]: '#1e6ce8', [EmploymentApplicationStatus.Viewed]: '#6366f1',
      [EmploymentApplicationStatus.InterviewScheduled]: '#0891b2', [EmploymentApplicationStatus.InterviewCompleted]: '#7c3aed',
      [EmploymentApplicationStatus.Offered]: '#10b981', [EmploymentApplicationStatus.Rejected]: '#ef4444', [EmploymentApplicationStatus.Withdrawn]: '#94a3b8',
    };
    return m[s] ?? '#6b7280';
  }

  getTypeLabel(t: EmploymentJobType): string {
    const m: Record<number, string> = { [EmploymentJobType.FullTime]: '全职', [EmploymentJobType.Internship]: '实习', [EmploymentJobType.PartTime]: '兼职', [EmploymentJobType.Apprenticeship]: '学徒' };
    return m[t] || '其他';
  }

  getTypeColor(t: EmploymentJobType): string {
    const m: Record<number, string> = { [EmploymentJobType.FullTime]: '#1e6ce8', [EmploymentJobType.Internship]: '#00b7ff', [EmploymentJobType.PartTime]: '#10b981', [EmploymentJobType.Apprenticeship]: '#f59e0b' };
    return m[t] || '#6b7280';
  }

  getSkillTags(item: JobPostingDto): string[] {
    if (!item.skillTags) return [];
    return item.skillTags.split(/[,,;;\n]/).map(s => s.trim()).filter(Boolean);
  }

  getBenefitItems(item: JobPostingDto): string[] {
    if (!item.benefits) return [];
    return item.benefits.split(/[,,;;\n]/).map(s => s.trim()).filter(Boolean);
  }

  coverGradient(item: JobPostingDto): string {
    const p = ['#1e6ce8', '#0c4cb8', '#1d4ed8', '#2563eb'];
    const key = item.id || '';
    let h = 0; for (let i = 0; i < key.length; i++) h = (h * 31 + key.charCodeAt(i)) | 0;
    return p[Math.abs(h) % p.length];
  }

  /** 当前进度阶段 0=none 1=已投递 2=面试 3=完成 4=录用 */
  stage(): number {
    const s = this.job()?.applicationStatus;
    if (s == null) return 0;
    const m: Record<number,number> = {
      [EmploymentApplicationStatus.Submitted]:1, [EmploymentApplicationStatus.Viewed]:1,
      [EmploymentApplicationStatus.InterviewScheduled]:2, [EmploymentApplicationStatus.InterviewCompleted]:3,
      [EmploymentApplicationStatus.Offered]:4, [EmploymentApplicationStatus.Rejected]:-1, [EmploymentApplicationStatus.Withdrawn]:0,
    };
    return m[s] ?? 0;
  }
}
