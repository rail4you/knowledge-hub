import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { EmploymentApplicationStatus, EmploymentJobType, EmploymentService, JobPostingDto, StudentResumeDto } from '../../employment/employment.service';

@Component({
  selector: 'app-student-job-detail',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, RouterLink, NzIconModule, NzSpinModule],
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
  readonly applicationStatuses = EmploymentApplicationStatus;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.message.error('岗位不存在');
      this.router.navigate(['/student/employment/jobs']);
      return;
    }

    this.loading.set(true);
    this.employmentService.getJob(id).subscribe({
      next: job => {
        this.job.set(job);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载岗位失败');
        this.router.navigate(['/student/employment/jobs']);
      },
    });

    this.employmentService.getMyResumeList().subscribe(items => {
      const list = items || [];
      this.resumes.set(list);
      const defaultResume = list.find(x => x.isDefault) || list[0];
      if (defaultResume) {
        this.selectedResumeId.set(defaultResume.id);
      }
    });
  }

  apply(): void {
    const job = this.job();
    if (!job) {
      return;
    }
    if (job.hasApplied) {
      this.message.warning('您已投递过该岗位');
      return;
    }
    if (!this.selectedResumeId()) {
      this.message.warning('请选择一份简历后再投递');
      return;
    }
    if (this.resumes().length === 0) {
      this.message.warning('请先创建简历后再投递');
      return;
    }

    this.submitting.set(true);
    this.employmentService.createApplication({
      jobPostingId: job.id,
      resumeId: this.selectedResumeId(),
      coverLetter: this.coverLetter() || undefined,
    }).subscribe({
      next: () => {
        this.message.success('投递成功，可在【我的投递】中查看进度');
        this.submitting.set(false);
        this.employmentService.getJob(job.id).subscribe(item => this.job.set(item));
      },
      error: () => {
        this.submitting.set(false);
        this.message.error('投递失败，请稍后再试');
      },
    });
  }

  goMyResumes(): void {
    this.router.navigate(['/student/employment/my-resumes']);
  }

  getTypeLabel(type: EmploymentJobType): string {
    const labels: Record<number, string> = {
      [EmploymentJobType.FullTime]: '全职',
      [EmploymentJobType.Internship]: '实习',
      [EmploymentJobType.PartTime]: '兼职',
      [EmploymentJobType.Apprenticeship]: '学徒',
    };
    return labels[type] || '其他';
  }

  getTypeColor(type: EmploymentJobType): string {
    const colors: Record<number, string> = {
      [EmploymentJobType.FullTime]: '#1e6ce8',
      [EmploymentJobType.Internship]: '#00b7ff',
      [EmploymentJobType.PartTime]: '#10b981',
      [EmploymentJobType.Apprenticeship]: '#f59e0b',
    };
    return colors[type] || '#6b7280';
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
    const palettes = [
      '#1e6ce8',
      '#0c4cb8',
      '#1d4ed8',
      '#2563eb',
    ];
    const key = item.id || item.title || '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
    return palettes[Math.abs(hash) % palettes.length];
  }
}
