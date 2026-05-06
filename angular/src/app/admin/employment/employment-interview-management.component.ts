import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { CreateUpdateInterviewScheduleDto, EmploymentInterviewResult, EmploymentService, InterviewScheduleDto, JobApplicationDto, RecordInterviewResultDto } from '../../employment/employment.service';

@Component({
  selector: 'app-employment-interview-management',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzCardModule, NzInputModule, NzModalModule, NzSelectModule, NzTableModule],
  templateUrl: './employment-interview-management.component.html',
  styleUrls: ['./employment-interview-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentInterviewManagementComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly applications = signal<JobApplicationDto[]>([]);
  readonly interviews = signal<InterviewScheduleDto[]>([]);
  readonly results = EmploymentInterviewResult;
  scheduleVisible = false;
  resultVisible = false;
  selectedApplication: JobApplicationDto | null = null;
  resultTarget: InterviewScheduleDto | null = null;
  scheduleForm: CreateUpdateInterviewScheduleDto = this.createScheduleForm();
  resultForm: RecordInterviewResultDto = this.createResultForm();

  ngOnInit(): void {
    this.reload();
  }

  createScheduleForm(): CreateUpdateInterviewScheduleDto {
    return {
      applicationId: '',
      interviewerName: '',
      interviewerPhone: '',
      scheduledAt: new Date().toISOString(),
      location: '',
      meetingUrl: '',
      note: '',
    };
  }

  createResultForm(): RecordInterviewResultDto {
    return {
      result: EmploymentInterviewResult.Pending,
      summary: '',
      resultComment: '',
    };
  }

  reload(): void {
    this.employmentService.getJobApplicationList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.applications.set(result.items || []));

    this.employmentService.getInterviewList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.interviews.set(result.items || []));
  }

  openSchedule(item: JobApplicationDto): void {
    this.selectedApplication = item;
    this.scheduleForm = {
      applicationId: item.id,
      interviewerName: '',
      interviewerPhone: '',
      scheduledAt: new Date().toISOString(),
      location: '',
      meetingUrl: '',
      note: '',
    };
    this.scheduleVisible = true;
  }

  saveSchedule(): void {
    this.employmentService.scheduleInterview(this.scheduleForm).subscribe({
      next: () => {
        this.scheduleVisible = false;
        this.message.success('面试已预约');
        this.reload();
      },
      error: () => this.message.error('预约失败'),
    });
  }

  openResult(item: InterviewScheduleDto): void {
    this.resultTarget = item;
    this.resultForm = {
      result: item.result,
      summary: item.summary || '',
      resultComment: item.resultComment || '',
    };
    this.resultVisible = true;
  }

  saveResult(): void {
    if (!this.resultTarget) {
      return;
    }

    this.employmentService.recordInterviewResult(this.resultTarget.id, this.resultForm).subscribe({
      next: () => {
        this.resultVisible = false;
        this.message.success('面试结果已记录');
        this.reload();
      },
      error: () => this.message.error('保存失败'),
    });
  }
}
