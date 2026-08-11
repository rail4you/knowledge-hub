import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  CreateUpdateEmploymentOutcomeDto,
  EmploymentApplicationStatus,
  EmploymentOutcomeDto,
  EmploymentOutcomeStatus,
  EmploymentService,
  JobApplicationDto,
} from '../../employment/employment.service';

@Component({
  selector: 'app-student-my-outcomes',
  standalone: true,
  imports: [
    CommonModule, DatePipe, FormsModule, RouterLink,
    NzIconModule, NzSpinModule, NzEmptyModule, NzModalModule, NzInputModule, NzButtonModule,
    NzSelectModule, NzSwitchModule, NzTagModule,
  ],
  templateUrl: './student-my-outcomes.component.html',
  styleUrls: ['./student-my-outcomes.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyOutcomesComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly configState = inject(ConfigStateService);
  private readonly route = inject(ActivatedRoute);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly items = signal<EmploymentOutcomeDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly statuses = EmploymentOutcomeStatus;
  readonly statusesArr = [
    { value: EmploymentOutcomeStatus.Intention, label: '就业意向' },
    { value: EmploymentOutcomeStatus.Signed, label: '已签约' },
    { value: EmploymentOutcomeStatus.Employed, label: '已就业' },
    { value: EmploymentOutcomeStatus.FurtherStudy, label: '升学' },
    { value: EmploymentOutcomeStatus.Entrepreneurship, label: '创业' },
    { value: EmploymentOutcomeStatus.Unemployed, label: '待就业' },
  ];

  readonly primaryCount = computed(() => this.items().filter(x => x.isPrimary).length);

  // ===== 新增 / 编辑弹窗 =====
  modalVisible = false;
  editingId: string | null = null;
  form: CreateUpdateEmploymentOutcomeDto = this.emptyForm();

  // ===== 从已录用投递导入 =====
  importVisible = false;
  importLoading = false;
  offeredApps = signal<JobApplicationDto[]>([]);

  ngOnInit(): void {
    this.reload();
    const importId = this.route.snapshot.queryParamMap.get('import');
    if (importId) {
      this.openImportWithSelection(importId);
    }
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

  reload(): void {
    this.loading.set(true);
    this.employmentService.getOutcomeList({ skipCount: 0, maxResultCount: 100 }).subscribe({
      next: result => {
        this.items.set(result.items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载就业去向失败');
      },
    });
  }

  private currentUserId(): string {
    return this.configState.getAll()?.currentUser?.id ?? '';
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.emptyForm();
    this.form.studentId = this.currentUserId();
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
    this.openImportWithSelection('');
  }

  openImportWithSelection(appId: string): void {
    this.importVisible = true;
    this.importLoading = true;
    this.offeredApps.set([]);
    this.employmentService.getMyApplicationList({
      status: EmploymentApplicationStatus.Offered,
      skipCount: 0,
      maxResultCount: 50,
    }).subscribe({
      next: result => {
        const apps = result.items || [];
        this.offeredApps.set(apps);
        this.importLoading = false;
        if (appId) {
          const target = apps.find(a => a.id === appId);
          if (target) {
            this.importApplication(target);
          } else {
            this.message.info('该投递已非「已录用」状态，请重新选择');
          }
        }
      },
      error: () => {
        this.importLoading = false;
        this.message.error('加载已录用投递失败');
      },
    });
  }

  importApplication(app: JobApplicationDto): void {
    this.form = this.emptyForm();
    this.form.studentId = this.currentUserId();
    this.form.applicationId = app.id;
    this.form.employerName = app.companyName || '';
    this.form.jobTitle = app.jobTitle || '';
    this.form.status = EmploymentOutcomeStatus.Employed;
    this.importVisible = false;
    this.editingId = null;
    this.modalVisible = true;
  }

  // ===== 保存 =====
  save(): void {
    if (!this.form.employerName.trim()) {
      this.message.warning('请填写去向单位');
      return;
    }
    if (!this.form.jobTitle.trim()) {
      this.message.warning('请填写岗位名称');
      return;
    }
    if (!this.form.studentId) {
      this.message.warning('无法识别当前学生身份，请重新登录');
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
    this.employmentService.saveOutcome({
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
    }).subscribe({
      next: () => {
        this.message.success('已设为主要就业去向');
        this.reload();
      },
      error: () => this.message.error('设置失败'),
    });
  }

  delete(item: EmploymentOutcomeDto): void {
    this.modal.confirm({
      nzTitle: '确认删除该就业去向？',
      nzContent: `【${item.employerName} · ${item.jobTitle}】删除后无法恢复。`,
      nzOkText: '确认删除',
      nzOkType: 'primary',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: () => {
        return new Promise<void>(resolve => {
          this.employmentService.deleteOutcome(item.id).subscribe({
            next: () => {
              this.message.success('就业去向已删除');
              this.reload();
              resolve();
            },
            error: () => {
              this.message.error('删除失败');
              resolve();
            },
          });
        });
      },
    });
  }

  statusLabel(s: EmploymentOutcomeStatus): string {
    return this.statusesArr.find(x => x.value === s)?.label ?? '未知';
  }

  statusColor(s: EmploymentOutcomeStatus): string {
    switch (s) {
      case EmploymentOutcomeStatus.Intention: return 'blue';
      case EmploymentOutcomeStatus.Signed: return 'cyan';
      case EmploymentOutcomeStatus.Employed: return 'green';
      case EmploymentOutcomeStatus.FurtherStudy: return 'purple';
      case EmploymentOutcomeStatus.Entrepreneurship: return 'gold';
      case EmploymentOutcomeStatus.Unemployed: return 'default';
      default: return 'default';
    }
  }

  private toDateTimeLocal(value: string): string {
    if (!value) return '';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }
}
