import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { EmploymentApplicationStatus, EmploymentService, InterviewScheduleDto, JobApplicationDto } from './employment.service';

@Component({
  selector: 'app-my-applications',
  standalone: true,
  imports: [CommonModule, RouterLink, NzCardModule, NzTableModule, NzTagModule, DatePipe],
  templateUrl: './my-applications.component.html',
  styleUrls: ['./my-applications.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyApplicationsComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);

  readonly items = signal<JobApplicationDto[]>([]);
  readonly interviewMap = signal<Record<string, InterviewScheduleDto>>({});

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.employmentService.getMyApplicationList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => {
      this.items.set(result.items || []);
      this.loadInterviews(result.items || []);
    });
  }

  loadInterviews(apps: JobApplicationDto[]): void {
    const hasInterviewApps = apps.some(a =>
      a.status === EmploymentApplicationStatus.InterviewScheduled ||
      a.status === EmploymentApplicationStatus.InterviewCompleted
    );
    if (!hasInterviewApps) return;

    this.employmentService.getInterviewList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: result => {
        const map: Record<string, InterviewScheduleDto> = {};
        for (const iv of (result.items || [])) {
          if (!map[iv.applicationId] || new Date(iv.creationTime) > new Date(map[iv.applicationId].creationTime)) {
            map[iv.applicationId] = iv;
          }
        }
        this.interviewMap.set(map);
      },
    });
  }

  getStatusLabel(status: EmploymentApplicationStatus): string {
    const labels: Record<number, string> = {
      [EmploymentApplicationStatus.Submitted]: '已投递',
      [EmploymentApplicationStatus.Viewed]: '已查看',
      [EmploymentApplicationStatus.InterviewScheduled]: '等待面试',
      [EmploymentApplicationStatus.InterviewCompleted]: '面试完成',
      [EmploymentApplicationStatus.Offered]: '已录用',
      [EmploymentApplicationStatus.Rejected]: '未通过',
      [EmploymentApplicationStatus.Withdrawn]: '已撤回',
    };
    return labels[status] || '未知';
  }

  getStatusColor(status: EmploymentApplicationStatus): string {
    const colors: Record<number, string> = {
      [EmploymentApplicationStatus.Submitted]: 'blue',
      [EmploymentApplicationStatus.Viewed]: 'purple',
      [EmploymentApplicationStatus.InterviewScheduled]: 'cyan',
      [EmploymentApplicationStatus.InterviewCompleted]: 'geekblue',
      [EmploymentApplicationStatus.Offered]: 'green',
      [EmploymentApplicationStatus.Rejected]: 'red',
      [EmploymentApplicationStatus.Withdrawn]: 'default',
    };
    return colors[status] ?? 'default';
  }
}
