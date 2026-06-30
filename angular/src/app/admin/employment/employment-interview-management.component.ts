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
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ConfigStateService } from '@abp/ng.core';
import { CreateUpdateInterviewScheduleDto, EmploymentInterviewResult, EmploymentService, InterviewScheduleDto, InterviewerCandidateDto, JobApplicationDto, RecordInterviewResultDto } from '../../employment/employment.service';

@Component({
  selector: 'app-employment-interview-management',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzCardModule, NzInputModule, NzModalModule, NzSelectModule, NzTableModule, NzTagModule],
  templateUrl: './employment-interview-management.component.html',
  styleUrls: ['./employment-interview-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentInterviewManagementComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly configState = inject(ConfigStateService);
  private readonly message = inject(NzMessageService);

  /** 当前登录用户 ID */
  private get currentUserId(): string | undefined {
    const cu = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    return cu?.['id'] as string | undefined;
  }

  readonly applications = signal<JobApplicationDto[]>([]);
  readonly interviews = signal<InterviewScheduleDto[]>([]);
  /** P1-8：面试官候选列表（教师/HR） */
  readonly interviewerOptions = signal<InterviewerCandidateDto[]>([]);
  readonly results = EmploymentInterviewResult;
  scheduleVisible = false;
  resultVisible = false;
  selectedApplication: JobApplicationDto | null = null;
  resultTarget: InterviewScheduleDto | null = null;
  scheduleForm: CreateUpdateInterviewScheduleDto = this.createScheduleForm();
  resultForm: RecordInterviewResultDto = this.createResultForm();

  ngOnInit(): void {
    this.reload();
    this.loadInterviewerOptions();
  }

  createScheduleForm(): CreateUpdateInterviewScheduleDto {
    const now = new Date();
    const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    return {
      applicationId: '',
      interviewerId: undefined,
      interviewerName: '',
      interviewerPhone: '',
      scheduledAt: local,
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
    }).subscribe({
      next: result => this.applications.set(result.items || []),
      error: () => this.message.error('加载投递列表失败'),
    });

    this.employmentService.getInterviewList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe({
      next: result => this.interviews.set(result.items || []),
      error: () => this.message.error('加载面试记录失败'),
    });
  }

  /** P1-8：拉取面试官候选（教师/HR/管理员） */
  loadInterviewerOptions(): void {
    this.employmentService.getInterviewerCandidates().subscribe({
      next: result => this.interviewerOptions.set(result || []),
      error: () => {
        // 静默失败，下拉回退到自由文本输入
        this.interviewerOptions.set([]);
      },
    });
  }

  /** 下拉选中面试官 → 自动补全 Name（用户仍可手动改） */
  onInterviewerPicked(id: string | null | undefined): void {
    if (!id) {
      this.scheduleForm.interviewerId = undefined;
      return;
    }
    this.scheduleForm.interviewerId = id;
    const opt = this.interviewerOptions().find(o => o.id === id);
    if (opt && !this.scheduleForm.interviewerName?.trim()) {
      this.scheduleForm.interviewerName = opt.name;
    }
  }

  openSchedule(item: JobApplicationDto): void {
    this.selectedApplication = item;
    // P1-8：如果是教师/HR 等面试官角色，自动预填当前用户为面试官
    const myId = this.currentUserId;
    const myOption = myId ? this.interviewerOptions().find(o => o.id === myId) : undefined;
    // datetime-local 格式：yyyy-MM-ddTHH:mm
    const now = new Date();
    const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    this.scheduleForm = {
      applicationId: item.id,
      interviewerId: myOption ? myId : undefined,
      interviewerName: myOption ? myOption.name : '',
      interviewerPhone: '',
      scheduledAt: local,
      location: '',
      meetingUrl: '',
      note: '',
    };
    this.scheduleVisible = true;
  }

  saveSchedule(): void {
    // 将 datetime-local 格式转回 ISO 8601
    const form = { ...this.scheduleForm, scheduledAt: new Date(this.scheduleForm.scheduledAt).toISOString() };
    this.employmentService.scheduleInterview(form).subscribe({
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
