import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { ConfigStateService } from '@abp/ng.core';
import {
  CompleteInterviewDto,
  CreateUpdateInterviewScheduleDto,
  EmploymentApplicationStatus,
  EmploymentInterviewResult,
  EmploymentService,
  InterviewerCandidateDto,
  InterviewScheduleDto,
  JobApplicationDto,
  RecordInterviewResultDto,
} from '../../employment/employment.service';

@Component({
  selector: 'app-employment-interview-management',
  standalone: true,
  imports: [
    CommonModule, DatePipe, FormsModule,
    NzButtonModule, NzCardModule, NzInputModule, NzModalModule,
    NzSelectModule, NzTableModule, NzTagModule, NzIconModule,
    NzTooltipModule, NzEmptyModule, NzSpinModule, NzPopconfirmModule,
  ],
  templateUrl: './employment-interview-management.component.html',
  styleUrls: ['./employment-interview-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentInterviewManagementComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly configState = inject(ConfigStateService);
  private readonly message = inject(NzMessageService);

  private get currentUserId(): string | undefined {
    const cu = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    return cu?.['id'] as string | undefined;
  }

  readonly applications = signal<JobApplicationDto[]>([]);
  readonly interviews = signal<InterviewScheduleDto[]>([]);
  readonly interviewerOptions = signal<InterviewerCandidateDto[]>([]);
  readonly loading = signal(false);

  readonly appStatus = EmploymentApplicationStatus;
  readonly interviewResults = EmploymentInterviewResult;

  // --- Schedule Modal ---
  scheduleVisible = false;
  selectedApplication: JobApplicationDto | null = null;
  scheduleForm: CreateUpdateInterviewScheduleDto = this.emptyScheduleForm();

  // --- Complete Modal ---
  completeVisible = false;
  completeTarget: InterviewScheduleDto | null = null;
  completeMessage = '';

  // --- Result Modal ---
  resultVisible = false;
  resultTarget: InterviewScheduleDto | null = null;
  resultForm: RecordInterviewResultDto = this.emptyResultForm();

  // --- Update Modal ---
  updateVisible = false;
  updateTarget: InterviewScheduleDto | null = null;
  updateForm: CreateUpdateInterviewScheduleDto = this.emptyScheduleForm();

  ngOnInit(): void {
    this.reload();
    this.loadInterviewerOptions();
  }

  emptyScheduleForm(): CreateUpdateInterviewScheduleDto {
    return {
      applicationId: '',
      interviewerId: undefined,
      interviewerName: '',
      interviewerPhone: '',
      scheduledAt: this.toLocalDatetime(new Date()),
      location: '',
      meetingUrl: '',
      note: '',
    };
  }

  emptyResultForm(): RecordInterviewResultDto {
    return { result: EmploymentInterviewResult.Pending, summary: '', resultComment: '' };
  }

  toLocalDatetime(d: Date): string {
    return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  }

  reload(): void {
    this.loading.set(true);
    this.employmentService.getJobApplicationList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: result => { this.applications.set(result.items || []); this.loading.set(false); },
      error: () => { this.message.error('加载投递列表失败'); this.loading.set(false); },
    });
    this.employmentService.getInterviewList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: result => this.interviews.set(result.items || []),
      error: () => this.message.error('加载面试记录失败'),
    });
  }

  loadInterviewerOptions(): void {
    this.employmentService.getInterviewerCandidates().subscribe({
      next: r => this.interviewerOptions.set(r || []),
      error: () => this.interviewerOptions.set([]),
    });
  }

  onInterviewerPicked(id: string | null | undefined): void {
    if (!id) { this.scheduleForm.interviewerId = undefined; return; }
    this.scheduleForm.interviewerId = id;
    const opt = this.interviewerOptions().find(o => o.id === id);
    if (opt && !this.scheduleForm.interviewerName?.trim()) {
      this.scheduleForm.interviewerName = opt.name;
    }
  }

  onUpdateInterviewerPicked(id: string | null | undefined): void {
    if (!id) { this.updateForm.interviewerId = undefined; return; }
    this.updateForm.interviewerId = id;
    const opt = this.interviewerOptions().find(o => o.id === id);
    if (opt && !this.updateForm.interviewerName?.trim()) {
      this.updateForm.interviewerName = opt.name;
    }
  }

  // =============== Schedule Interview ===============

  openSchedule(app: JobApplicationDto): void {
    this.selectedApplication = app;
    const myId = this.currentUserId;
    const myOption = myId ? this.interviewerOptions().find(o => o.id === myId) : undefined;
    this.scheduleForm = {
      applicationId: app.id,
      interviewerId: myOption ? myId : undefined,
      interviewerName: myOption ? myOption.name : '',
      interviewerPhone: '',
      scheduledAt: this.toLocalDatetime(new Date()),
      location: '',
      meetingUrl: '',
      note: '',
    };
    this.scheduleVisible = true;
  }

  saveSchedule(): void {
    const form = { ...this.scheduleForm, scheduledAt: new Date(this.scheduleForm.scheduledAt).toISOString() };
    this.employmentService.scheduleInterview(form).subscribe({
      next: () => { this.scheduleVisible = false; this.message.success('面试已安排'); this.reload(); },
      error: () => this.message.error('安排面试失败'),
    });
  }

  // =============== Complete Interview ===============

  openComplete(item: InterviewScheduleDto): void {
    this.completeTarget = item;
    this.completeMessage = item.completionMessage || '';
    this.completeVisible = true;
  }

  saveComplete(): void {
    if (!this.completeTarget) return;
    if (!this.completeMessage.trim()) { this.message.warning('请填写完成提示信息'); return; }
    this.employmentService.completeInterview(this.completeTarget.id, { completionMessage: this.completeMessage.trim() }).subscribe({
      next: () => { this.completeVisible = false; this.message.success('面试已标记为完成'); this.reload(); },
      error: () => this.message.error('操作失败'),
    });
  }

  // =============== Update Interview ===============

  openUpdate(item: InterviewScheduleDto): void {
    this.updateTarget = item;
    this.updateForm = {
      applicationId: item.applicationId,
      interviewerId: item.interviewerId,
      interviewerName: item.interviewerName,
      interviewerPhone: item.interviewerPhone || '',
      scheduledAt: this.toLocalDatetime(new Date(item.scheduledAt)),
      location: item.location || '',
      meetingUrl: item.meetingUrl || '',
      note: item.note || '',
    };
    this.updateVisible = true;
  }

  saveUpdate(): void {
    if (!this.updateTarget) return;
    const form = { ...this.updateForm, scheduledAt: new Date(this.updateForm.scheduledAt).toISOString() };
    this.employmentService.updateInterview(this.updateTarget.id, form).subscribe({
      next: () => { this.updateVisible = false; this.message.success('面试信息已更新'); this.reload(); },
      error: () => this.message.error('更新失败'),
    });
  }

  // =============== Record Result ===============

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
    if (!this.resultTarget) return;
    this.employmentService.recordInterviewResult(this.resultTarget.id, this.resultForm).subscribe({
      next: () => { this.resultVisible = false; this.message.success('面试结果已记录'); this.reload(); },
      error: () => this.message.error('保存失败'),
    });
  }

  // =============== Delete Interview ===============

  deleteInterview(item: InterviewScheduleDto): void {
    this.employmentService.deleteInterview(item.id).subscribe({
      next: () => { this.message.success('面试记录已删除'); this.reload(); },
      error: () => this.message.error('删除失败'),
    });
  }

  // =============== Helpers ===============

  getAppStatusLabel(s: EmploymentApplicationStatus): string {
    const m: Record<number, string> = {
      [EmploymentApplicationStatus.Submitted]: '已投递',
      [EmploymentApplicationStatus.Viewed]: '已查看',
      [EmploymentApplicationStatus.InterviewScheduled]: '等待面试',
      [EmploymentApplicationStatus.InterviewCompleted]: '面试完成',
      [EmploymentApplicationStatus.Offered]: '已录用',
      [EmploymentApplicationStatus.Rejected]: '未通过',
      [EmploymentApplicationStatus.Withdrawn]: '已撤回',
    };
    return m[s] ?? '未知';
  }

  getAppStatusColor(s: EmploymentApplicationStatus): string {
    const m: Record<number, string> = {
      [EmploymentApplicationStatus.Submitted]: 'blue',
      [EmploymentApplicationStatus.Viewed]: 'purple',
      [EmploymentApplicationStatus.InterviewScheduled]: 'cyan',
      [EmploymentApplicationStatus.InterviewCompleted]: 'geekblue',
      [EmploymentApplicationStatus.Offered]: 'green',
      [EmploymentApplicationStatus.Rejected]: 'red',
      [EmploymentApplicationStatus.Withdrawn]: 'default',
    };
    return m[s] ?? 'default';
  }

  getResultLabel(r: EmploymentInterviewResult): string {
    const m: Record<number, string> = {
      [EmploymentInterviewResult.Pending]: '待定',
      [EmploymentInterviewResult.Passed]: '通过',
      [EmploymentInterviewResult.Deferred]: '待补充',
      [EmploymentInterviewResult.Failed]: '未通过',
    };
    return m[r] ?? '未知';
  }

  getResultColor(r: EmploymentInterviewResult): string {
    const m: Record<number, string> = {
      [EmploymentInterviewResult.Pending]: 'default',
      [EmploymentInterviewResult.Passed]: 'green',
      [EmploymentInterviewResult.Deferred]: 'orange',
      [EmploymentInterviewResult.Failed]: 'red',
    };
    return m[r] ?? 'default';
  }

  /** 是否显示"完成面试"按钮：面试已安排但尚未完成，且结果仍为 Pending */
  canComplete(item: InterviewScheduleDto): boolean {
    return item.result === EmploymentInterviewResult.Pending && !item.completedAt;
  }

  /** 是否显示"回填结果"按钮：面试已完成，或结果非 Pending */
  canRecordResult(item: InterviewScheduleDto): boolean {
    return !!item.completedAt || item.result !== EmploymentInterviewResult.Pending;
  }
}
