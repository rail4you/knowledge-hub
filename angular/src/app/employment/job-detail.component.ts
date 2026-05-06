import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { EmploymentJobType, EmploymentService, JobPostingDto, StudentResumeDto } from './employment.service';

@Component({
  selector: 'app-employment-job-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, NzButtonModule, NzCardModule, NzInputModule, NzSelectModule],
  templateUrl: './job-detail.component.html',
  styleUrls: ['./job-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentJobDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly job = signal<JobPostingDto | null>(null);
  readonly resumes = signal<StudentResumeDto[]>([]);
  selectedResumeId = '';
  coverLetter = '';

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }

    this.employmentService.getJob(id).subscribe(job => this.job.set(job));
    this.employmentService.getMyResumeList().subscribe(items => {
      this.resumes.set(items || []);
      this.selectedResumeId = items?.find(x => x.isDefault)?.id || items?.[0]?.id || '';
    });
  }

  apply(): void {
    const job = this.job();
    if (!job || !this.selectedResumeId) {
      this.message.warning('请先准备默认简历后再投递');
      return;
    }

    this.employmentService.createApplication({
      jobPostingId: job.id,
      resumeId: this.selectedResumeId,
      coverLetter: this.coverLetter || undefined,
    }).subscribe({
      next: () => {
        this.message.success('投递成功');
        this.employmentService.getJob(job.id).subscribe(item => this.job.set(item));
      },
      error: () => this.message.error('投递失败'),
    });
  }

  getTypeLabel(type: EmploymentJobType): string {
    const labels: Record<number, string> = {
      [EmploymentJobType.FullTime]: '全职',
      [EmploymentJobType.Internship]: '实习',
      [EmploymentJobType.PartTime]: '兼职',
      [EmploymentJobType.Apprenticeship]: '学徒',
    };
    return labels[type] || '未知';
  }
}
