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
import { CreateUpdateJobPostingDto, EmploymentJobStatus, EmploymentJobType, EmploymentService, JobPostingDto } from '../../employment/employment.service';

@Component({
  selector: 'app-employment-job-management',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzCardModule, NzInputModule, NzModalModule, NzSelectModule, NzTableModule],
  templateUrl: './employment-job-management.component.html',
  styleUrls: ['./employment-job-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentJobManagementComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<JobPostingDto[]>([]);
  readonly statuses = EmploymentJobStatus;
  readonly jobTypes = EmploymentJobType;
  modalVisible = false;
  editingId: string | null = null;
  statusFilter?: EmploymentJobStatus;
  form: CreateUpdateJobPostingDto = this.createEmptyForm();

  ngOnInit(): void {
    this.reload();
  }

  createEmptyForm(): CreateUpdateJobPostingDto {
    return {
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

  reload(): void {
    this.employmentService.getManageJobList({
      status: this.statusFilter,
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.items.set(result.items || []));
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.createEmptyForm();
    this.modalVisible = true;
  }

  openEdit(item: JobPostingDto): void {
    this.editingId = item.id;
    this.form = {
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
    this.modalVisible = true;
  }

  save(): void {
    const request = this.editingId
      ? this.employmentService.updateJob(this.editingId, this.form)
      : this.employmentService.createJob(this.form);

    request.subscribe({
      next: () => {
        this.modalVisible = false;
        this.message.success('岗位已保存');
        this.reload();
      },
      error: () => this.message.error('保存失败'),
    });
  }

  review(item: JobPostingDto, status: EmploymentJobStatus): void {
    this.employmentService.reviewJob(item.id, {
      status,
      reviewComment: status === EmploymentJobStatus.Rejected ? '请完善岗位信息后重新提交' : '审核通过',
    }).subscribe({
      next: () => {
        this.message.success('审核状态已更新');
        this.reload();
      },
      error: () => this.message.error('审核失败'),
    });
  }

  delete(id: string): void {
    this.employmentService.deleteJob(id).subscribe({
      next: () => {
        this.message.success('岗位已删除');
        this.reload();
      },
      error: () => this.message.error('删除失败'),
    });
  }
}
