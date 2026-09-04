import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzUploadFile, NzUploadModule } from 'ng-zorro-antd/upload';
import { ResourceService } from '../proxy/resources/resource.service';
import { ResourceDto } from '../proxy/resources/models';
import { OssUploadService, OssUploadResultDto } from '../shared/oss-upload.service';
import {
  CreateDoubleHighEvidenceDto,
  CreateUpdateDoubleHighIndicatorDto,
  DoubleHighDataSourceType,
  DoubleHighEvidenceDto,
  DoubleHighEvidenceType,
  DoubleHighIndicatorDto,
  DoubleHighProjectDetailDto,
  DoubleHighProjectStatus,
  DoubleHighReportDto,
  DoubleHighService,
} from './double-high.service';

@Component({
  selector: 'app-double-high-project-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzDescriptionsModule,
    NzInputModule,
    NzIconModule,
    NzModalModule,
    NzProgressModule,
    NzSpinModule,
    NzRadioModule,
    NzSelectModule,
    NzTableModule,

    NzTagModule,
    NzUploadModule,
    RouterLink,
  ],
  templateUrl: './double-high-project-detail.component.html',
  styleUrls: ['./double-high-project-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DoubleHighProjectDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly doubleHighService = inject(DoubleHighService);
  private readonly resourceService = inject(ResourceService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);

  readonly project = signal<DoubleHighProjectDetailDto | null>(null);
  readonly dataSources = DoubleHighDataSourceType;
  readonly evidenceTypes = DoubleHighEvidenceType;
  readonly statuses = DoubleHighProjectStatus;
  readonly activeTab = signal(0);
  readonly tabs = ['项目概述', '指标数据', '佐证材料', '导出信息'];
  readonly tabIcons = ['dashboard', 'bars', 'paper-clip', 'file-excel'];

  indicatorVisible = false;
  editingIndicatorId: string | null = null;
  indicatorForm: CreateUpdateDoubleHighIndicatorDto = this.createEmptyIndicatorForm();
  latestValueDraft: number | null = null;
  latestNoteDraft = '';

  evidenceVisible = false;
  editingEvidenceId: string | null = null;
  evidenceForm: CreateDoubleHighEvidenceDto = this.createEmptyEvidenceForm();
  resourceOptions = signal<ResourceDto[]>([]);
  resourceLoading = signal(false);
  evidenceUploading = signal(false);
  evidenceFileList: NzUploadFile[] = [];
  uploadedAttachment: OssUploadResultDto | null = null;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.load(id);
    }
  }

  loadResources(): void {
    this.resourceLoading.set(true);
    this.resourceService.getList({ skipCount: 0, maxResultCount: 500 }).subscribe({
      next: result => this.resourceOptions.set(result.items || []),
      error: () => this.message.error('加载资源列表失败'),
      complete: () => this.resourceLoading.set(false),
    });
  }

  createEmptyIndicatorForm(): CreateUpdateDoubleHighIndicatorDto {
    return {
      categoryName: '',
      indicatorCode: '',
      name: '',
      description: '',
      unit: '',
      dataSourceType: DoubleHighDataSourceType.Manual,
      targetValue: undefined,
      weight: 1,
      sortOrder: 0,
    };
  }

  createEmptyEvidenceForm(): CreateDoubleHighEvidenceDto {
    return {
      projectId: '',
      indicatorId: '',
      title: '',
      description: '',
      evidenceType: DoubleHighEvidenceType.ResourceLink,
      resourceId: undefined,
      attachmentUrl: '',
      externalLink: '',
      sortOrder: 1,
    };
  }

  load(id: string): void {
    this.doubleHighService.getDetail(id).subscribe(detail => {
      this.project.set(detail);
    });
  }

  openCreateIndicator(): void {
    this.editingIndicatorId = null;
    this.indicatorForm = this.createEmptyIndicatorForm();
    this.latestValueDraft = null;
    this.latestNoteDraft = '';
    this.indicatorVisible = true;
  }

  openEditIndicator(item: DoubleHighIndicatorDto): void {
    this.editingIndicatorId = item.id;
    this.indicatorForm = {
      parentId: item.parentId,
      categoryName: item.categoryName,
      indicatorCode: item.indicatorCode,
      name: item.name,
      description: item.description || '',
      unit: item.unit || '',
      dataSourceType: item.dataSourceType,
      targetValue: item.targetValue,
      weight: item.weight || 1,
      sortOrder: item.sortOrder,
    };
    this.latestValueDraft = item.latestValue?.value ?? null;
    this.latestNoteDraft = item.latestValue?.note || '';
    this.indicatorVisible = true;
  }

  saveIndicator(): void {
    const project = this.project();
    if (!project) {
      return;
    }

    const request = this.editingIndicatorId
      ? this.doubleHighService.updateIndicator(this.editingIndicatorId, this.indicatorForm)
      : this.doubleHighService.addIndicator(project.id, this.indicatorForm);

    request.subscribe({
      next: saved => {
        const finish = () => {
          this.indicatorVisible = false;
          this.message.success(this.editingIndicatorId ? '指标已更新' : '指标已添加');
          this.load(project.id);
        };

        // 编辑时若填写了最新值，则一并手工填报（自动采集指标也允许手工覆盖，作为最新值）
        if (
          this.editingIndicatorId &&
          this.latestValueDraft !== null &&
          this.latestValueDraft !== undefined
        ) {
          this.doubleHighService.saveManualValue({
            indicatorId: saved.id,
            value: this.latestValueDraft,
            note: this.latestNoteDraft || undefined,
          }).subscribe({
            next: () => finish(),
            error: err => this.showApiError(err, '最新值保存失败'),
          });
        } else {
          finish();
        }
      },
      error: err => this.showApiError(err, this.editingIndicatorId ? '指标更新失败' : '指标保存失败'),
    });
  }

  deleteIndicator(id: string): void {
    this.doubleHighService.deleteIndicator(id).subscribe({
      next: () => {
        this.message.success('指标已删除');
        const project = this.project();
        if (project) {
          this.load(project.id);
        }
      },
      error: err => this.showApiError(err, '指标删除失败'),
    });
  }

  openEvidence(indicator?: DoubleHighIndicatorDto): void {
    const project = this.project();
    if (!project) {
      return;
    }

    this.editingEvidenceId = null;
    this.evidenceForm = {
      projectId: project.id,
      indicatorId: indicator?.id ?? '',
      title: '',
      description: '',
      evidenceType: DoubleHighEvidenceType.ResourceLink,
      resourceId: undefined,
      attachmentUrl: '',
      externalLink: '',
      sortOrder: project.evidences.length + 1,
    };
    this.uploadedAttachment = null;
    this.evidenceFileList = [];
    this.evidenceVisible = true;
    this.loadResources();
  }

  openEditEvidence(item: DoubleHighEvidenceDto): void {
    const project = this.project();
    if (!project) {
      return;
    }

    this.editingEvidenceId = item.id;
    this.evidenceForm = {
      projectId: project.id,
      indicatorId: item.indicatorId,
      title: item.title,
      description: item.description || '',
      evidenceType: item.evidenceType,
      resourceId: item.resourceId,
      attachmentUrl: item.attachmentUrl || '',
      externalLink: item.externalLink || '',
      sortOrder: item.sortOrder,
    };
    this.uploadedAttachment = this.evidenceForm.attachmentUrl
      ? { url: this.evidenceForm.attachmentUrl, objectKey: '', originalFileName: '', size: 0 }
      : null;
    this.evidenceFileList = this.evidenceForm.attachmentUrl
      ? [{
          uid: '-1',
          name: this.evidenceForm.attachmentUrl.split('/').pop() || this.evidenceForm.attachmentUrl,
          status: 'done',
          url: this.evidenceForm.attachmentUrl,
        } as NzUploadFile]
      : [];
    this.evidenceVisible = true;
    this.loadResources();
  }

  onEvidenceTypeChange(): void {
    // 三种类型互斥：切换类型时清空其余类型的字段
    this.evidenceForm.resourceId = undefined;
    this.evidenceForm.attachmentUrl = '';
    this.evidenceForm.externalLink = '';
    this.uploadedAttachment = null;
    this.evidenceFileList = [];
  }

  beforeEvidenceUpload = (file: NzUploadFile): boolean => {
    const rawFile = file as unknown as File;
    if (rawFile.size > 50 * 1024 * 1024) {
      this.message.error('附件文件不能超过 50MB');
      return false;
    }
    this.evidenceUploading.set(true);
    this.ossUploadService.uploadFile(rawFile).subscribe({
      next: res => {
        this.uploadedAttachment = res;
        this.evidenceForm.attachmentUrl = res.url;
        this.evidenceUploading.set(false);
        this.evidenceFileList = [{
          uid: '-1',
          name: res.originalFileName,
          status: 'done',
          url: res.url,
        } as NzUploadFile];
        this.message.success(`附件上传成功：${res.originalFileName}`);
      },
      error: err => {
        this.evidenceUploading.set(false);
        this.evidenceFileList = [];
        this.message.error('附件上传失败：' + (err?.error?.error?.message || err?.message || '未知错误'));
      },
    });
    return false;
  };

  removeEvidenceAttachment = (): boolean => {
    this.uploadedAttachment = null;
    this.evidenceForm.attachmentUrl = '';
    this.evidenceFileList = [];
    return true;
  };

  saveEvidence(): void {
    if (!this.evidenceForm.indicatorId) {
      this.message.warning('请选择关联指标');
      return;
    }

    if (this.evidenceForm.evidenceType === DoubleHighEvidenceType.ResourceLink && !this.evidenceForm.resourceId) {
      this.message.warning('请选择关联资源');
      return;
    }
    if (this.evidenceForm.evidenceType === DoubleHighEvidenceType.AttachmentLink && !this.evidenceForm.attachmentUrl) {
      this.message.warning('请上传附件文件');
      return;
    }
    if (this.evidenceForm.evidenceType === DoubleHighEvidenceType.ExternalLink && !this.evidenceForm.externalLink) {
      this.message.warning('请填写外部链接 URL');
      return;
    }

    const request = this.editingEvidenceId
      ? this.doubleHighService.updateEvidence(this.editingEvidenceId, this.evidenceForm)
      : this.doubleHighService.addEvidence(this.evidenceForm);

    request.subscribe({
      next: () => {
        this.evidenceVisible = false;
        this.message.success(this.editingEvidenceId ? '佐证材料已更新' : '佐证材料已添加');
        const project = this.project();
        if (project) {
          this.load(project.id);
        }
      },
      error: err => this.showApiError(err, this.editingEvidenceId ? '佐证材料更新失败' : '佐证材料保存失败'),
    });
  }

  deleteEvidence(id: string): void {
    this.doubleHighService.deleteEvidence(id).subscribe({
      next: () => {
        this.message.success('佐证材料已删除');
        const project = this.project();
        if (project) {
          this.load(project.id);
        }
      },
      error: err => this.showApiError(err, '删除失败'),
    });
  }

  exportReport(): void {
    const project = this.project();
    if (!project) {
      return;
    }

    this.doubleHighService.exportReport(project.id).subscribe({
      next: blob => {
        this.downloadBlob(blob, `${project.batchCode}_双高评估报表.xlsx`);
        this.load(project.id);
      },
      error: err => this.showApiError(err, '导出失败'),
    });
  }

  downloadReport(report: DoubleHighReportDto): void {
    this.doubleHighService.downloadReport(report.id).subscribe({
      next: blob => this.downloadBlob(blob, report.reportName || '双高评估报表.xlsx'),
      error: err => this.showApiError(err, '下载失败'),
    });
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    window.URL.revokeObjectURL(url);
  }

  getStatusLabel(status: DoubleHighProjectStatus): string {
    const labels: Record<number, string> = {
      [DoubleHighProjectStatus.Draft]: '草稿',
      [DoubleHighProjectStatus.Active]: '进行中',
      [DoubleHighProjectStatus.Closed]: '已关闭',
    };
    return labels[status] || '未知';
  }

  formatLocalDate(value?: string): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (isNaN(date.getTime())) {
      return value;
    }
    // 使用本地时间显示，格式：2026-07-01 17:00
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  getSourceLabel(type: DoubleHighDataSourceType): string {
    const labels: Record<number, string> = {
      [DoubleHighDataSourceType.Manual]: '手工填报',
      [DoubleHighDataSourceType.ResourceCount]: '资源数量',
      [DoubleHighDataSourceType.CourseCount]: '课程数量',
      [DoubleHighDataSourceType.MicroMajorCount]: '微专业数量',
      [DoubleHighDataSourceType.PracticumProjectCount]: '实训项目数量',
      [DoubleHighDataSourceType.NewsArticleCount]: '资讯数量',
      [DoubleHighDataSourceType.MicroMajorEnrollmentCount]: '微专业报名量',
      [DoubleHighDataSourceType.PracticumEnrollmentCount]: '实训参与量',
    };
    return labels[type] || '未知';
  }

  getEvidenceTypeLabel(type: DoubleHighEvidenceType): string {
    const labels: Record<number, string> = {
      [DoubleHighEvidenceType.ResourceLink]: '资源链接',
      [DoubleHighEvidenceType.AttachmentLink]: '附件链接',
      [DoubleHighEvidenceType.ExternalLink]: '外部链接',
    };
    return labels[type] || '未知';
  }

  getResourceName(resourceId?: string): string {
    if (!resourceId) return '-';
    const found = this.resourceOptions().find(r => r.id === resourceId);
    return found?.name || '-';
  }

  private showApiError(err: any, fallback: string): void {
    const detail =
      err?.error?.error?.message ||
      err?.error?.message ||
      err?.message ||
      '未知错误';
    console.error('[DoubleHigh]', fallback, err);
    this.message.error(`${fallback}：${detail}`);
  }
}
