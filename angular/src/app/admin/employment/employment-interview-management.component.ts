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
import { TenantUserService } from '../../proxy/application/identity/tenant-user.service';
import { GetTenantUsersInput, TenantUserDto } from '../../proxy/application/identity/models';

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
  private readonly tenantUserService = inject(TenantUserService);
  private readonly message = inject(NzMessageService);

  readonly applications = signal<JobApplicationDto[]>([]);
  readonly interviews = signal<InterviewScheduleDto[]>([]);
  /** P1-8：面试官候选列表（教师/HR） */
  readonly interviewerOptions = signal<{ id: string; name: string }[]>([]);
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
    return {
      applicationId: '',
      interviewerId: undefined,
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

  /** P1-8：拉取教师/HR 列表作为面试官候选（已包含登录名/邮箱/姓名） */
  loadInterviewerOptions(): void {
    const input: GetTenantUsersInput = {
      filter: '',
      sorting: 'name',
      skipCount: 0,
      maxResultCount: 200,
    };
    this.tenantUserService.getList(input).subscribe({
      next: result => {
        const items = (result.items || [])
          .filter(u => this.isInterviewerCandidate(u))
          .map(u => ({ id: u.id, name: this.resolveDisplayName(u) }));
        this.interviewerOptions.set(items);
      },
      error: () => {
        // 静默失败，下拉回退到自由文本输入
        this.interviewerOptions.set([]);
      },
    });
  }

  /** 候选规则：教师 / HR / 联盟管理员（这些角色通常担任面试官） */
  private isInterviewerCandidate(u: TenantUserDto): boolean {
    const roleType = (u as any).extraProperties?.['RoleType'];
    return roleType === 2 /* Teacher */ || roleType === 3 /* EnterpriseUser */ || roleType === 1 /* LeagueAdmin */ || roleType === 0 /* SchoolAdmin */;
  }

  private resolveDisplayName(u: TenantUserDto): string {
    const n = [u.name, u.surname].filter(x => !!x && !!x.trim()).join(' ').trim();
    if (n) return n;
    return u.userName || (u as any).email || u.id;
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
    this.scheduleForm = {
      applicationId: item.id,
      interviewerId: undefined,
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
