import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { PermissionService } from '@abp/ng.core';
import { CertificateService } from '../../proxy/certificates/certificate.service';
import type {
  CertificateApplicationDto,
  CertificateTemplateDto,
  CreateUpdateCertificateTemplateDto,
} from '../../proxy/certificates/dtos/models';
import { CertificateApplicationStatus } from '../../proxy/certificates/certificate-application-status.enum';
import { OssUploadService } from '../../shared/oss-upload.service';
import { CertificateLayer } from '../../shared/certificate/certificate.models';
import { CertificateLayerEditorComponent } from '../../shared/certificate/certificate-layer-editor.component';
import { CertificateIssueComposerComponent } from '../../shared/certificate/certificate-issue-composer.component';

/**
 * 平台课程证书中心（PRD 第 9 节）。
 *
 * <para>
 * 「证书模板」标签页：平台管理员统一配置证书模板（校徽位、学员姓名、课程名称、结业时间、
 * 证书编号、防伪二维码等字段），院校 / 老师不可自定义。
 * </para>
 * <para>
 * 「申请审核」标签页：学员申请 → 平台管理员审核 → 审核通过后发放电子证书。
 * </para>
 */
@Component({
  selector: 'app-certificate-management',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    FormsModule,
    NzButtonModule,
    NzDividerModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSpinModule,
    NzSwitchModule,
    NzTableModule,
    NzTabsModule,
    NzTagModule,
    NzEmptyModule,
    CertificateLayerEditorComponent,
    CertificateIssueComposerComponent,
  ],
  templateUrl: './certificate-management.component.html',
  styleUrls: ['./certificate-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertificateManagementComponent implements OnInit {
  private readonly certificateService = inject(CertificateService);
  private readonly permissionService = inject(PermissionService);
  private readonly ossUpload = inject(OssUploadService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly templates = signal<CertificateTemplateDto[]>([]);
  readonly applications = signal<CertificateApplicationDto[]>([]);
  readonly applicationStatus = signal<CertificateApplicationStatus | null>(CertificateApplicationStatus.Pending);
  readonly filter = signal('');
  /** 搜索框双向绑定（回车或点击查询时生效）。 */
  filterInput = '';

  // ── 模板编辑 ────────────────────────────────────────────
  readonly templateModalVisible = signal(false);
  readonly templateEditorVisible = signal(false);
  readonly editingTemplateId = signal<string | null>(null);
  readonly uploading = signal(false);
  templateForm: CreateUpdateCertificateTemplateDto = this.emptyTemplateForm();
  templateLayers: CertificateLayer[] = [];

  // ── 发证弹窗 ────────────────────────────────────────────
  readonly issueModalVisible = signal(false);
  readonly issuing = signal(false);
  issueTarget: CertificateApplicationDto | null = null;

  /** 是否拥有平台侧模板管理权限（库所有者只有审核发证权限）。 */
  readonly canManageTemplate = computed(() =>
    this.permissionService.getGrantedPolicy('KnowledgeHub.Certificates.ManageTemplate'),
  );

  readonly pendingCount = computed(() => this.applications().filter(a => a.status === CertificateApplicationStatus.Pending).length);

  readonly statusLabels: Record<number, string> = {
    0: '待审核',
    1: '已发证',
    2: '已驳回',
    3: '已撤回',
  };

  ngOnInit(): void {
    this.loadTemplates();
    this.loadApplications();
  }

  emptyTemplateForm(): CreateUpdateCertificateTemplateDto {    return {
      name: '',
      imageUrl: '',
      layersJson: '',
      isDefault: false,
      isEnabled: true,
      sortOrder: 0,
      description: '',
    };
  }

  /** 模板占位符数量。 */
  layerCount(layersJson?: string | null): number {
    return this.parseLayers(layersJson).length;
  }

  /** 日期格式化。 */
  formatDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  loadTemplates(): void {
    this.loading.set(true);
    this.certificateService.getTemplates().subscribe({
      next: list => {
        this.templates.set(list || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载证书模板失败');
      },
    });
  }

  loadApplications(): void {
    this.loading.set(true);
    this.certificateService
      .getApplications({
        status: this.applicationStatus() ?? undefined,
        filter: this.filter() || undefined,
        skipCount: 0,
        maxResultCount: 200,
      })
      .subscribe({
        next: res => {
          this.applications.set(res?.items || []);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载证书申请失败');
        },
      });
  }

  // ── 模板维护 ────────────────────────────────────────────

  openTemplateCreate(): void {
    if (!this.canManageTemplate()) {
      this.message.warning('证书模板由平台统一配置，院校 / 老师不可自定义。');
      return;
    }
    this.editingTemplateId.set(null);
    this.templateForm = this.emptyTemplateForm();
    this.templateLayers = [];
    this.templateModalVisible.set(true);
  }

  openTemplateEdit(item: CertificateTemplateDto): void {
    if (!this.canManageTemplate()) {
      this.message.warning('证书模板由平台统一配置，院校 / 老师不可自定义。');
      return;
    }
    this.editingTemplateId.set(item.id);
    this.templateForm = {
      name: item.name,
      imageUrl: item.imageUrl,
      layersJson: item.layersJson ?? '',
      isDefault: item.isDefault,
      isEnabled: item.isEnabled,
      sortOrder: item.sortOrder,
      description: item.description ?? '',
    };
    this.templateLayers = this.parseLayers(item.layersJson);
    this.templateModalVisible.set(true);
  }

  private parseLayers(layersJson?: string | null): CertificateLayer[] {
    if (!layersJson) return [];
    try {
      const parsed = JSON.parse(layersJson);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }

  /** 打开图层编辑器（需先有底图） */
  openLayerEditor(): void {
    if (!this.templateForm.imageUrl) {
      this.message.warning('请先上传证书底图');
      return;
    }
    this.templateEditorVisible.set(true);
  }

  onLayersSaved(layers: CertificateLayer[]): void {
    this.templateLayers = layers;
    this.templateForm.layersJson = JSON.stringify(layers);
    this.templateEditorVisible.set(false);
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      this.message.error('只能上传图片文件');
      return;
    }
    this.uploading.set(true);
    this.ossUpload.uploadImage(file).subscribe({
      next: res => {
        this.templateForm.imageUrl = res.url;
        this.uploading.set(false);
        this.message.success('底图上传成功');
      },
      error: () => {
        this.uploading.set(false);
        this.message.error('底图上传失败');
      },
    });
  }

  saveTemplate(): void {
    if (!this.templateForm.name?.trim()) {
      this.message.warning('请填写模板名称');
      return;
    }
    if (!this.templateForm.imageUrl) {
      this.message.warning('请上传证书底图');
      return;
    }

    const payload: CreateUpdateCertificateTemplateDto = {
      ...this.templateForm,
      layersJson: this.templateForm.layersJson || (this.templateLayers.length ? JSON.stringify(this.templateLayers) : ''),
    };

    const editingId = this.editingTemplateId();
    const request = editingId
      ? this.certificateService.updateTemplate(editingId, payload)
      : this.certificateService.createTemplate(payload);

    request.subscribe({
      next: () => {
        this.templateModalVisible.set(false);
        this.message.success('证书模板已保存');
        this.loadTemplates();
      },
      error: (err: any) => this.message.error(err?.error?.error?.message || '保存失败'),
    });
  }

  deleteTemplate(item: CertificateTemplateDto): void {
    if (!this.canManageTemplate()) {
      this.message.warning('证书模板由平台统一配置，院校 / 老师不可自定义。');
      return;
    }
    this.certificateService.deleteTemplate(item.id).subscribe({
      next: () => {
        this.message.success('已删除');
        this.loadTemplates();
      },
      error: (err: any) => this.message.error(err?.error?.error?.message || '删除失败'),
    });
  }

  toggleTemplateDefault(item: CertificateTemplateDto): void {
    if (!this.canManageTemplate()) {
      return;
    }
    this.certificateService
      .updateTemplate(item.id, { name: item.name, imageUrl: item.imageUrl, isDefault: !item.isDefault })
      .subscribe({
        next: () => {
          this.message.success('已更新默认模板');
          this.loadTemplates();
        },
        error: (err: any) => this.message.error(err?.error?.error?.message || '操作失败'),
      });
  }

  toggleTemplateEnabled(item: CertificateTemplateDto): void {
    if (!this.canManageTemplate()) {
      return;
    }
    this.certificateService
      .updateTemplate(item.id, { name: item.name, imageUrl: item.imageUrl, isEnabled: !item.isEnabled })
      .subscribe({
        next: () => {
          this.message.success('已更新状态');
          this.loadTemplates();
        },
        error: (err: any) => this.message.error(err?.error?.error?.message || '操作失败'),
      });
  }

  // ── 申请审核 ────────────────────────────────────────────

  openIssue(item: CertificateApplicationDto): void {
    this.issueTarget = item;
    this.issueModalVisible.set(true);
  }

  onIssued(input: any): void {
    if (!this.issueTarget) return;
    this.issuing.set(true);
    this.certificateService.approve(input).subscribe({
      next: () => {
        this.issuing.set(false);
        this.issueModalVisible.set(false);
        this.message.success('证书已发放，并归档到主建老师的「其他学员」目录');
        this.loadApplications();
      },
      error: (err: any) => {
        this.issuing.set(false);
        this.message.error(err?.error?.error?.message || '发证失败');
      },
    });
  }

  reject(item: CertificateApplicationDto): void {
    this.certificateService.reject({ applicationId: item.id, reviewRemark: '材料不齐，请补充后重新申请' }).subscribe({
      next: () => {
        this.message.success('已驳回申请');
        this.loadApplications();
      },
      error: (err: any) => this.message.error(err?.error?.error?.message || '操作失败'),
    });
  }
}
