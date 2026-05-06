import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTableModule } from 'ng-zorro-antd/table';
import { EmploymentApplicationStatus, EmploymentService, JobApplicationDto } from './employment.service';

@Component({
  selector: 'app-my-applications',
  standalone: true,
  imports: [CommonModule, RouterLink, NzCardModule, NzTableModule],
  templateUrl: './my-applications.component.html',
  styleUrls: ['./my-applications.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyApplicationsComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);

  readonly items = signal<JobApplicationDto[]>([]);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.employmentService.getMyApplicationList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.items.set(result.items || []));
  }

  getStatusLabel(status: EmploymentApplicationStatus): string {
    const labels: Record<number, string> = {
      [EmploymentApplicationStatus.Submitted]: '已投递',
      [EmploymentApplicationStatus.Viewed]: '已查看',
      [EmploymentApplicationStatus.InterviewScheduled]: '已约面试',
      [EmploymentApplicationStatus.Offered]: '已录用',
      [EmploymentApplicationStatus.Rejected]: '未通过',
      [EmploymentApplicationStatus.Withdrawn]: '已撤回',
    };
    return labels[status] || '未知';
  }
}
