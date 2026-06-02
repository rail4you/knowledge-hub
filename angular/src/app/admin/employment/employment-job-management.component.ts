import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  CreateUpdateJobPostingDto,
  EmploymentJobStatus,
  EmploymentJobType,
  EmploymentService,
  JobPostingDto,
} from '../../employment/employment.service';

const STATUS_LABEL: Record<number, string> = {
  [EmploymentJobStatus.Draft]: '草稿',
  [EmploymentJobStatus.PendingReview]: '待审',
  [EmploymentJobStatus.Published]: '已发布',
  [EmploymentJobStatus.Rejected]: '已驳回',
  [EmploymentJobStatus.Closed]: '已关闭',
};

const STATUS_COLOR: Record<number, string> = {
  [EmploymentJobStatus.Draft]: 'default',
  [EmploymentJobStatus.PendingReview]: 'gold',
  [EmploymentJobStatus.Published]: 'green',
  [EmploymentJobStatus.Rejected]: 'red',
  [EmploymentJobStatus.Closed]: 'volcano',
};

const JOB_TYPE_LABEL: Record<number, string> = {
  [EmploymentJobType.FullTime]: '全职',
  [EmploymentJobType.Internship]: '实习',
  [EmploymentJobType.PartTime]: '兼职',
  [EmploymentJobType.Apprenticeship]: '学徒',
};

@Component({
  selector: 'app-employment-job-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NzButtonModule,
    NzCardModule,
    NzDatePickerModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './employment-job-management.component.html',
  styleUrls: ['./employment-job-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentJobManagementComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<JobPostingDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly statuses = EmploymentJobStatus;
  readonly jobTypes = EmploymentJobType;

  // 表格筛选
  statusFilter?: EmploymentJobStatus;
  keyword = '';
  locationFilter = '';
  jobTypeFilter?: EmploymentJobType;

  // 分页
  pageIndex = 1;
  pageSize = 10;

  // 模态框
  modalVisible = false;
  editingId: string | null = null;
  form: CreateUpdateJobPostingDto = this.createEmptyForm();
  formDeadline: Date | null = null;

  ngOnInit(): void {
    this.reload();
  }

  createEmptyForm(): CreateUpdateJobPostingDto {
    return {
      companyName: '',
      industry: '',
      title: '',
      summary: '',
      description: '',
      location: '',
      address: '',
      jobType: EmploymentJobType.FullTime,
      educationRequirement: '',
      salaryRange: '',
      recruitmentCount: 1,
      skillTags: '',
      benefits: '',
      contactName: '',
      contactPhone: '',
      contactEmail: '',
      deadline: undefined,
      status: EmploymentJobStatus.Draft,
    };
  }

  private toDate(value: string | undefined | null): Date | null {
    if (!value) return null;
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? null : d;
  }

  reload(): void {
    this.loading.set(true);
    this.employmentService
      .getManageJobList({
        status: this.statusFilter,
        filter: this.keyword || undefined,
        location: this.locationFilter || undefined,
        jobType: this.jobTypeFilter,
        skipCount: (this.pageIndex - 1) * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: result => {
          this.items.set(result.items || []);
          this.totalCount.set(result.totalCount || 0);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('岗位列表加载失败');
        },
      });
  }

  onPageIndexChange(index: number): void {
    this.pageIndex = index;
    this.reload();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.pageIndex = 1;
    this.reload();
  }

  onFilterChange(): void {
    this.pageIndex = 1;
    this.reload();
  }

  search(): void {
    this.onFilterChange();
  }

  resetFilter(): void {
    this.keyword = '';
    this.locationFilter = '';
    this.jobTypeFilter = undefined;
    this.statusFilter = undefined;
    this.onFilterChange();
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.createEmptyForm();
    this.formDeadline = null;
    this.modalVisible = true;
  }

  openEdit(item: JobPostingDto): void {
    this.editingId = item.id;
    this.form = {
      companyName: item.companyName,
      industry: item.industry || '',
      title: item.title,
      summary: item.summary || '',
      description: item.description,
      location: item.location || '',
      address: item.address || '',
      jobType: item.jobType,
      educationRequirement: item.educationRequirement || '',
      salaryRange: item.salaryRange || '',
      recruitmentCount: item.recruitmentCount,
      skillTags: item.skillTags || '',
      benefits: item.benefits || '',
      contactName: item.contactName || '',
      contactPhone: item.contactPhone || '',
      contactEmail: item.contactEmail || '',
      deadline: item.deadline,
      status: item.status,
    };
    this.formDeadline = this.toDate(item.deadline);
    this.modalVisible = true;
  }

  save(): void {
    if (this.saving()) {
      return;
    }

    this.saving.set(true);
    const deadline = this.formDeadline ? this.formDeadline.toISOString() : undefined;
    const payload: CreateUpdateJobPostingDto = {
      ...this.form,
      title: this.form.title?.trim() || '',
      description: this.form.description?.trim() || '',
      deadline,
    };

    const request = this.editingId
      ? this.employmentService.updateJob(this.editingId, payload)
      : this.employmentService.createJob(payload);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.modalVisible = false;
        this.message.success(this.editingId ? '岗位已更新' : '岗位已创建');
        this.reload();
      },
      error: err => {
        this.saving.set(false);
        this.message.error(this.extractErrorMessage(err, '保存失败'));
      },
    });
  }

  review(item: JobPostingDto, status: EmploymentJobStatus): void {
    this.employmentService
      .reviewJob(item.id, {
        status,
        reviewComment: status === EmploymentJobStatus.Rejected ? '请完善岗位信息后重新提交' : '审核通过',
      })
      .subscribe({
        next: () => {
          this.message.success('审核状态已更新');
          this.reload();
        },
        error: err => this.message.error(this.extractErrorMessage(err, '审核失败')),
      });
  }

  delete(id: string): void {
    this.employmentService.deleteJob(id).subscribe({
      next: () => {
        this.message.success('岗位已删除');
        this.reload();
      },
      error: err => this.message.error(this.extractErrorMessage(err, '删除失败')),
    });
  }

  // 枚举 -> 中文标签
  getStatusLabel(status: EmploymentJobStatus): string {
    return STATUS_LABEL[status] ?? '未知';
  }

  getStatusColor(status: EmploymentJobStatus): string {
    return STATUS_COLOR[status] ?? 'default';
  }

  getTypeLabel(type: EmploymentJobType): string {
    return JOB_TYPE_LABEL[type] ?? '未知';
  }

  // 按钮显隐：仅在可执行的状态显示
  canReview(item: JobPostingDto, target: EmploymentJobStatus): boolean {
    if (target === EmploymentJobStatus.Published) {
      return (
        item.status === EmploymentJobStatus.Draft ||
        item.status === EmploymentJobStatus.PendingReview ||
        item.status === EmploymentJobStatus.Rejected
      );
    }
    if (target === EmploymentJobStatus.Rejected) {
      return (
        item.status === EmploymentJobStatus.Draft ||
        item.status === EmploymentJobStatus.PendingReview
      );
    }
    if (target === EmploymentJobStatus.Closed) {
      return item.status === EmploymentJobStatus.Published;
    }
    return false;
  }

  canReopen(item: JobPostingDto): boolean {
    return item.status === EmploymentJobStatus.Closed;
  }

  private extractErrorMessage(err: any, fallback: string): string {
    return (
      err?.error?.error?.message ||
      err?.error?.message ||
      err?.message ||
      fallback
    );
  }
}
