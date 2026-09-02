import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import {
  CreateUpdateEmploymentOutcomeDto,
  EmploymentApplicationStatus,
  EmploymentOutcomeDto,
  EmploymentOutcomeImportResultDto,
  EmploymentOutcomeStatus,
  EmploymentOutcomeStudentDto,
  EmploymentService,
  JobApplicationDto,
} from '../../employment/employment.service';

@Component({
  selector: 'app-employment-outcome-management',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzSpinModule,
    NzSwitchModule,
    NzTableModule,
    NzTagModule,
    NzUploadModule,
  ],
  templateUrl: './employment-outcome-management.component.html',
  styleUrls: ['./employment-outcome-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentOutcomeManagementComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<EmploymentOutcomeDto[]>([]);
  readonly students = signal<EmploymentOutcomeStudentDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly totalCount = signal(0);
  readonly statuses = EmploymentOutcomeStatus;

  // 筛选
  keyword = '';
  statusFilter?: EmploymentOutcomeStatus;
  studentFilter?: string;

  // 分页
  pageIndex = 1;
  pageSize = 10;

  // 新增 / 编辑弹窗
  modalVisible = false;
  editingId: string | null = null;
  form: CreateUpdateEmploymentOutcomeDto = this.emptyForm();

  // 从已录用投递导入
  importVisible = false;
  readonly importLoading = signal(false);
  offeredApps = signal<JobApplicationDto[]>([]);

  // xlsx 批量导入
  xlsxImportVisible = false;
  importing = false;
  importFileList: NzUploadFile[] = [];
  importResult: EmploymentOutcomeImportResultDto | null = null;

  ngOnInit(): void {
    this.loadStudents();
    this.reload();
  }

  emptyForm(): CreateUpdateEmploymentOutcomeDto {
    return {
      studentId: '',
      applicationId: undefined,
      employerName: '',
      jobTitle: '',
      status: EmploymentOutcomeStatus.Intention,
      employmentType: '校园招聘',
      region: '',
      salaryRange: '',
      startDate: undefined,
      confirmedAt: new Date().toISOString(),
      remark: '',
      isPrimary: true,
    };
  }

  loadStudents(): void {
    this.employmentService.getOutcomeStudents().subscribe({
      next: list => this.students.set(list || []),
      error: () => this.message.error('加载学生列表失败'),
    });
  }

  reload(): void {
    this.loading.set(true);
    this.employmentService
      .getOutcomeList({
        studentId: this.studentFilter,
        status: this.statusFilter,
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
          this.message.error('就业去向加载失败');
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
    this.statusFilter = undefined;
    this.studentFilter = undefined;
    this.onFilterChange();
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.emptyForm();
    this.modalVisible = true;
  }

  openEdit(item: EmploymentOutcomeDto): void {
    this.editingId = item.id;
    this.form = {
      id: item.id,
      studentId: item.studentId,
      applicationId: item.applicationId,
      employerName: item.employerName,
      jobTitle: item.jobTitle,
      status: item.status,
      employmentType: item.employmentType || '',
      region: item.region || '',
      salaryRange: item.salaryRange || '',
      startDate: item.startDate ? this.toDateTimeLocal(item.startDate) : undefined,
      confirmedAt: item.confirmedAt || new Date().toISOString(),
      remark: item.remark || '',
      isPrimary: item.isPrimary,
    };
    this.modalVisible = true;
  }

  closeModal(): void {
    this.modalVisible = false;
    this.editingId = null;
  }

  // ===== 从已录用投递导入 =====
  openImport(): void {
    const studentId = this.form.studentId;
    if (!studentId) {
      this.message.warning('请先选择学生');
      return;
    }
    this.importVisible = true;
    this.importLoading.set(true);
    this.offeredApps.set([]);
    this.employmentService
      .getJobApplicationList({
        studentId,
        status: EmploymentApplicationStatus.Offered,
        skipCount: 0,
        maxResultCount: 50,
      })
      .subscribe({
        next: result => {
          this.offeredApps.set(result.items || []);
          this.importLoading.set(false);
        },
        error: () => {
          this.importLoading.set(false);
          this.message.error('加载该学生的已录用投递失败');
        },
      });
  }

  importApplication(app: JobApplicationDto): void {
    this.form.applicationId = app.id;
    this.form.employerName = app.companyName || this.form.employerName;
    this.form.jobTitle = app.jobTitle || this.form.jobTitle;
    this.form.status = EmploymentOutcomeStatus.Employed;
    this.importVisible = false;
  }

  // ===== xlsx 批量导入 =====
  openXlsxImport(): void {
    this.xlsxImportVisible = true;
    this.importFileList = [];
    this.importResult = null;
  }

  beforeXlsxImportUpload = (file: NzUploadFile): boolean => {
    const name = (file.name || '').toLowerCase();
    if (!name.endsWith('.xlsx')) {
      this.message.warning('仅支持 .xlsx 文件');
      return false;
    }
    this.importFileList = [file];
    return false;
  };

  downloadTemplate(): void {
    this.employmentService.getOutcomeImportTemplate().subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `就业去向导入模板_${this.todayStr()}.xlsx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      },
      error: err => this.message.error(this.extractErrorMessage(err, '模板下载失败')),
    });
  }

  importXlsx(): void {
    const file = this.importFileList[0];
    if (!file) {
      this.message.warning('请先选择要导入的 xlsx 文件');
      return;
    }

    this.importing = true;
    this.importResult = null;
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      const base64 = dataUrl.split(',')[1] || '';
      this.employmentService
        .importOutcomes({ fileBase64: base64, fileName: file.name })
        .subscribe({
          next: result => {
            this.importing = false;
            this.importResult = result;
            const ok = result.successCount ?? 0;
            const fail = result.failCount ?? 0;
            if (fail > 0) {
              this.message.warning(`导入完成：成功 ${ok} 条，失败 ${fail} 条，详见下方明细`);
            } else {
              this.message.success(`导入完成：成功 ${ok} 条`);
            }
            this.reload();
          },
          error: err => {
            this.importing = false;
            this.importResult = null;
            this.message.error(this.extractErrorMessage(err, '导入失败'));
          },
        });
    };
    reader.onerror = () => {
      this.importing = false;
      this.message.error('读取文件失败，请重试');
    };
    reader.readAsDataURL(file as any);
  }

  private todayStr(): string {
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}`;
  }

  // ===== 保存 =====
  save(): void {
    if (!this.form.studentId) {
      this.message.warning('请选择学生');
      return;
    }
    if (!this.form.employerName.trim()) {
      this.message.warning('请填写去向单位');
      return;
    }
    if (!this.form.jobTitle.trim()) {
      this.message.warning('请填写岗位名称');
      return;
    }

    this.saving.set(true);
    const payload: CreateUpdateEmploymentOutcomeDto = {
      ...this.form,
      id: this.editingId ?? undefined,
      employerName: this.form.employerName.trim(),
      jobTitle: this.form.jobTitle.trim(),
      employmentType: this.form.employmentType?.trim() || undefined,
      region: this.form.region?.trim() || undefined,
      salaryRange: this.form.salaryRange?.trim() || undefined,
      remark: this.form.remark?.trim() || undefined,
      confirmedAt: this.form.confirmedAt || new Date().toISOString(),
    };

    this.employmentService.saveOutcome(payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.message.success(this.editingId ? '就业去向已更新' : '就业去向已保存');
        this.closeModal();
        this.reload();
      },
      error: () => {
        this.saving.set(false);
        this.message.error('保存失败');
      },
    });
  }

  setPrimary(item: EmploymentOutcomeDto): void {
    if (item.isPrimary) return;
    this.employmentService
      .saveOutcome({
        id: item.id,
        studentId: item.studentId,
        applicationId: item.applicationId,
        employerName: item.employerName,
        jobTitle: item.jobTitle,
        status: item.status,
        employmentType: item.employmentType,
        region: item.region,
        salaryRange: item.salaryRange,
        startDate: item.startDate,
        confirmedAt: item.confirmedAt,
        remark: item.remark,
        isPrimary: true,
      })
      .subscribe({
        next: () => {
          this.message.success('已设为主要就业去向');
          this.reload();
        },
        error: () => this.message.error('设置失败'),
      });
  }

  delete(item: EmploymentOutcomeDto): void {
    this.employmentService.deleteOutcome(item.id).subscribe({
      next: () => {
        this.message.success('就业去向已删除');
        this.reload();
      },
      error: err => this.message.error(this.extractErrorMessage(err, '删除失败')),
    });
  }

  // 枚举 -> 中文
  statusLabel(s: EmploymentOutcomeStatus): string {
    const m: Record<number, string> = {
      [EmploymentOutcomeStatus.Intention]: '就业意向',
      [EmploymentOutcomeStatus.Signed]: '已签约',
      [EmploymentOutcomeStatus.Employed]: '已就业',
      [EmploymentOutcomeStatus.FurtherStudy]: '升学',
      [EmploymentOutcomeStatus.Entrepreneurship]: '创业',
      [EmploymentOutcomeStatus.Unemployed]: '待就业',
    };
    return m[s] ?? '未知';
  }

  statusColor(s: EmploymentOutcomeStatus): string {
    const m: Record<number, string> = {
      [EmploymentOutcomeStatus.Intention]: 'blue',
      [EmploymentOutcomeStatus.Signed]: 'cyan',
      [EmploymentOutcomeStatus.Employed]: 'green',
      [EmploymentOutcomeStatus.FurtherStudy]: 'purple',
      [EmploymentOutcomeStatus.Entrepreneurship]: 'gold',
      [EmploymentOutcomeStatus.Unemployed]: 'default',
    };
    return m[s] ?? 'default';
  }

  getOutcomeStatusClass(s: EmploymentOutcomeStatus): string {
    if (s === EmploymentOutcomeStatus.Employed) return 'status-tag employed';
    if (s === EmploymentOutcomeStatus.Intention) return 'status-tag intention';
    if (s === EmploymentOutcomeStatus.Signed) return 'status-tag signed';
    return 'status-tag intention';
  }

  /** 前端关键字过滤当前页（后端列表接口无 keyword 参数） */
  filteredItems(): EmploymentOutcomeDto[] {
    const k = this.keyword.trim().toLowerCase();
    if (!k) return this.items();
    return this.items().filter(item => {
      const fields = [item.studentName, item.employerName, item.jobTitle, item.region, item.employmentType];
      return fields.some(f => (f || '').toLowerCase().includes(k));
    });
  }

  private toDateTimeLocal(value: string): string {
    if (!value) return '';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }

  private extractErrorMessage(err: any, fallback: string): string {
    return err?.error?.error?.message || err?.error?.message || err?.message || fallback;
  }
}
