import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { EmploymentJobStatus, EmploymentJobType, EmploymentService, JobPostingDto } from './employment.service';

@Component({
  selector: 'app-employment-job-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, NzButtonModule, NzCardModule, NzInputModule, NzSelectModule, NzTagModule],
  templateUrl: './job-list.component.html',
  styleUrls: ['./job-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentJobListComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);

  readonly jobs = signal<JobPostingDto[]>([]);
  readonly jobTypes = EmploymentJobType;
  keyword = '';
  location = '';
  jobType?: EmploymentJobType;

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.employmentService.getPublishedJobList({
      filter: this.keyword || undefined,
      location: this.location || undefined,
      jobType: this.jobType,
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.jobs.set(result.items || []));
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

  getStatusLabel(status: EmploymentJobStatus): string {
    const labels: Record<number, string> = {
      [EmploymentJobStatus.Draft]: '草稿',
      [EmploymentJobStatus.PendingReview]: '待审',
      [EmploymentJobStatus.Published]: '已发布',
      [EmploymentJobStatus.Rejected]: '已驳回',
      [EmploymentJobStatus.Closed]: '已关闭',
    };
    return labels[status] || '未知';
  }
}
