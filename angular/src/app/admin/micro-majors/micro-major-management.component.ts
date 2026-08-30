import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpEventType, HttpRequest } from '@angular/common/http';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { OssUploadService, OssUploadResultDto } from '../../shared/oss-upload.service';
import {
  CertificateLayer,
  CreateUpdateMicroMajorDto,
  CreateUpdateMicroMajorCertificateTemplateDto,
  IssueCertificateDefaultsDto,
  IssueCertificateInput,
  MicroMajorCertificateDto,
  MicroMajorCertificateTemplateDto,
  MicroMajorDto,
  MicroMajorEnrollmentDto,
  MicroMajorEnrollmentStatus,
  MicroMajorService,
  MicroMajorStatus,
} from '../../micro-majors/micro-major.service';
import { CertificateLayerEditorComponent } from './certificate-layer-editor.component';
import { CertificateIssueComposerComponent } from './certificate-issue-composer.component';

@Component({
  selector: 'app-micro-major-management',
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
    NzProgressModule,
    NzSelectModule,
    NzSpinModule,
    NzSwitchModule,
    NzTableModule,
    NzUploadModule,
    CertificateLayerEditorComponent,
    CertificateIssueComposerComponent,
  ],
  templateUrl: './micro-major-management.component.html',
  styleUrls: ['./micro-major-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MicroMajorManagementComponent implements OnInit {
  private readonly httpClient = inject(HttpClient);
  private readonly microMajorService = inject(MicroMajorService);
  private readonly courseService = inject(CourseService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<MicroMajorDto[]>([]);
  readonly enrollments = signal<MicroMajorEnrollmentDto[]>([]);
  readonly courses = signal<CourseDto[]>([]);
  readonly statuses = MicroMajorStatus;
  readonly enrollmentStatuses = MicroMajorEnrollmentStatus;
  readonly enrollmentFilter = signal<number | null>(MicroMajorEnrollmentStatus.Pending);

  modalVisible = false;
  editingId: string | null = null;
  selectedCourseIds: string[] = [];
  form: CreateUpdateMicroMajorDto = this.createEmptyForm();

  // Cover upload state
  coverUploading = false;
  coverFileList: NzUploadFile[] = [];

  // Certificate template management modal
  templateModalVisible = false;
  templateMicroMajorId = '';
  templateMicroMajorTitle = '';
  readonly certificateTemplates = signal<MicroMajorCertificateTemplateDto[]>([]);
  templateName = '';
  templateImageUploading = false;
  templateImageUrl = '';
  readonly templateUploadProgress = signal(0);

  // Certificate issue modal
  certificateModalVisible = false;
  certificateEnrollmentId = '';
  issueEnrollment: MicroMajorEnrollmentDto | undefined;
  readonly issueTemplates = signal<MicroMajorCertificateTemplateDto[]>([]);
  readonly issueDefaults = signal<IssueCertificateDefaultsDto | undefined>(undefined);
  issueDefaultsLoading = false;
  issueTemplatesLoading = false;

  // Certificate layer editor modal
  layerEditorVisible = false;
  layerEditorImageUrl = '';
  layerEditorInitialLayers: CertificateLayer[] = [];
  layerEditingTemplateId: string | null = null;
  templateDraftLayers: CertificateLayer[] = [];

  // Certificate preview modal
  certificatePreviewVisible = false;
  certificatePreviewUrl = '';

  ngOnInit(): void {
    this.loadCourses();
    this.reload();
  }

  createEmptyForm(): CreateUpdateMicroMajorDto {
    return {
      title: '',
      summary: '',
      description: '',
      coverImageUrl: '',
      industryField: '',
      collaborationUnit: '',
      status: MicroMajorStatus.Draft,
      requiredCompletionRate: 100,
      isCertificateEnabled: true,
      courses: [],
    };
  }

  loadCourses(): void {
    this.courseService.getList({
      maxResultCount: 200,
      skipCount: 0,
    } as any).subscribe({
      next: result => this.courses.set(result.items || []),
    });
  }

  setEnrollmentFilter(status: number | null): void {
    this.enrollmentFilter.set(status);
    this.reload();
  }

  reload(): void {
    this.microMajorService.getList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe({
      next: result => this.items.set(result.items || []),
    });

    this.microMajorService.getEnrollmentList({
      skipCount: 0,
      maxResultCount: 100,
      status: this.enrollmentFilter() ?? undefined,
    }).subscribe({
      next: result => this.enrollments.set(result.items || []),
    });
  }

  openCreate(): void {
    this.editingId = null;
    this.selectedCourseIds = [];
    this.form = this.createEmptyForm();
    this.coverFileList = [];
    this.modalVisible = true;
  }

  openEdit(item: MicroMajorDto): void {
    this.editingId = item.id;
    this.form = {
      title: item.title,
      summary: item.summary || '',
      description: item.description || '',
      coverImageUrl: item.coverImageUrl || '',
      industryField: item.industryField || '',
      collaborationUnit: item.collaborationUnit || '',
      status: item.status,
      requiredCompletionRate: item.requiredCompletionRate,
      isCertificateEnabled: item.isCertificateEnabled,
      courses: [],
    };

    this.microMajorService.getDetail(item.id).subscribe({
      next: detail => {
        this.selectedCourseIds = detail.courses
          .sort((a, b) => a.sortOrder - b.sortOrder)
          .map(x => x.courseId);

        // Set cover file list for preview
        if (item.coverImageUrl) {
          this.coverFileList = [{
            uid: '-1',
            name: 'cover.jpg',
            status: 'done',
            url: item.coverImageUrl,
          }];
        } else {
          this.coverFileList = [];
        }

        this.modalVisible = true;
      },
    });
  }

  beforeCoverUpload = (rawFile: NzUploadFile): boolean => {
    // Validate type
    const allowedTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp'];
    if (!allowedTypes.includes(rawFile.type!)) {
      this.message.error('仅支持上传 JPG、PNG、GIF、WebP、BMP 格式的图片');
      return false;
    }
    // Validate size (10MB)
    if (rawFile.size! > 10 * 1024 * 1024) {
      this.message.error('图片大小不能超过 10MB');
      return false;
    }

    // Upload to OSS
    this.coverUploading = true;
    this.ossUploadService.uploadImage(rawFile as unknown as File).subscribe({
      next: (result) => {
        this.coverUploading = false;
        this.form.coverImageUrl = result.url;
        this.coverFileList = [{
          uid: result.objectKey,
          name: result.originalFileName,
          status: 'done',
          url: result.url,
        }];
        this.message.success('封面上传成功');
      },
      error: (err) => {
        this.coverUploading = false;
        this.coverFileList = [];
        this.message.error('封面上传失败: ' + (err?.error?.error?.message || err?.message || '未知错误'));
      },
    });
    return false; // prevent default upload behavior
  };

  removeCover = (): boolean => {
    this.form.coverImageUrl = '';
    this.coverFileList = [];
    return true;
  };

  save(): void {
    this.form.courses = this.selectedCourseIds.map((courseId, index) => ({
      courseId,
      sortOrder: index + 1,
      isCore: true,
    }));

    const request = this.editingId
      ? this.microMajorService.update(this.editingId, this.form)
      : this.microMajorService.create(this.form);

    request.subscribe({
      next: () => {
        this.modalVisible = false;
        this.message.success('微专业已保存');
        this.reload();
      },
      error: () => {
        this.message.error('微专业保存失败');
      },
    });
  }

  delete(id: string): void {
    this.microMajorService.delete(id).subscribe({
      next: () => {
        this.message.success('微专业已删除');
        this.reload();
      },
      error: () => {
        this.message.error('删除失败');
      },
    });
  }

  // ==================== 证书模板管理（微专业维度） ====================
  openTemplateModal(item: MicroMajorDto): void {
    this.templateMicroMajorId = item.id;
    this.templateMicroMajorTitle = item.title;
    this.templateName = '';
    this.templateImageUrl = '';
    this.templateDraftLayers = [];
    this.templateUploadProgress.set(0);
    this.certificateTemplates.set([]);
    this.loadCertificateTemplates(item.id);
    this.templateModalVisible = true;
  }

  closeTemplateModal(): void {
    this.templateModalVisible = false;
    this.templateMicroMajorId = '';
    this.templateImageUrl = '';
    this.templateDraftLayers = [];
    this.templateUploadProgress.set(0);
  }

  // 打开发放模板的占位符编辑器
  openLayerEditorForNew(): void {
    if (!this.templateImageUrl) {
      this.message.warning('请先上传证书图片再配置占位符');
      return;
    }
    this.layerEditorImageUrl = this.templateImageUrl;
    this.layerEditorInitialLayers = this.templateDraftLayers;
    this.layerEditingTemplateId = null;
    this.layerEditorVisible = true;
  }

  // 打开发放已有模板的占位符编辑器
  openLayerEditorForTemplate(tpl: MicroMajorCertificateTemplateDto): void {
    this.layerEditorImageUrl = tpl.imageUrl;
    this.layerEditorInitialLayers = tpl.layers || [];
    this.layerEditingTemplateId = tpl.id;
    this.layerEditorVisible = true;
  }

  closeLayerEditor(): void {
    this.layerEditorVisible = false;
    this.layerEditorImageUrl = '';
    this.layerEditorInitialLayers = [];
    this.layerEditingTemplateId = null;
  }

  onLayerEditorSaved(layers: CertificateLayer[]): void {
    // 新建模板时暂存图层，随“保存模板”一起提交
    if (!this.layerEditingTemplateId) {
      this.templateDraftLayers = layers;
      this.closeLayerEditor();
      return;
    }

    // 编辑已有模板：直接调用更新接口持久化占位符图层
    const tpl = this.certificateTemplates().find(x => x.id === this.layerEditingTemplateId);
    if (!tpl) {
      this.closeLayerEditor();
      return;
    }

    const input: CreateUpdateMicroMajorCertificateTemplateDto = {
      microMajorId: tpl.microMajorId,
      name: tpl.name,
      imageUrl: tpl.imageUrl,
      sortOrder: tpl.sortOrder,
      layers,
    };
    this.microMajorService.updateCertificateTemplate(tpl.id, input).subscribe({
      next: () => {
        this.message.success('占位符已保存');
        this.loadCertificateTemplates(this.templateMicroMajorId);
        this.closeLayerEditor();
      },
      error: (err) => {
        this.message.error('保存失败: ' + (err?.error?.error?.message || err?.message || '未知错误'));
      },
    });
  }

  loadCertificateTemplates(microMajorId: string): void {
    this.microMajorService.getCertificateTemplates(microMajorId).subscribe({
      next: result => this.certificateTemplates.set(result || []),
      error: () => this.message.error('证书模板加载失败'),
    });
  }

  async uploadTemplateImage(file: File): Promise<void> {
    const allowedTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp'];
    if (!allowedTypes.includes(file.type)) {
      this.message.error('仅支持上传 JPG、PNG、GIF、WebP、BMP 格式的图片');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.message.error('图片大小不能超过 10MB');
      return;
    }

    this.templateImageUploading = true;
    this.templateUploadProgress.set(0);

    const formData = new FormData();
    formData.append('file', file);

    const req = new HttpRequest('POST', '/api/oss-upload/image', formData, {
      reportProgress: true,
    });

    this.httpClient.request(req).subscribe({
      next: (event: any) => {
        if (event.type === HttpEventType.UploadProgress) {
          const progress = Math.round((100 * event.loaded) / (event.total || 1));
          this.templateUploadProgress.set(progress);
        } else if (event.type === HttpEventType.Response) {
          const result = event.body as OssUploadResultDto;
          this.templateImageUploading = false;
          this.templateUploadProgress.set(100);
          this.templateImageUrl = result.url;
          this.message.success('证书图片上传成功');
        }
      },
      error: (err: any) => {
        this.templateImageUploading = false;
        this.templateUploadProgress.set(0);
        this.message.error('上传失败: ' + (err?.error?.error?.message || err?.message || '未知错误'));
      },
    });
  }

  removeTemplateImage = (): void => {
    this.templateImageUrl = '';
    this.templateUploadProgress.set(0);
  };

  addCertificateTemplate(): void {
    if (!this.templateMicroMajorId) return;
    if (!this.templateName.trim()) {
      this.message.warning('请输入证书模板名称');
      return;
    }
    if (!this.templateImageUrl) {
      this.message.warning('请先上传证书图片');
      return;
    }

    const input: CreateUpdateMicroMajorCertificateTemplateDto = {
      microMajorId: this.templateMicroMajorId,
      name: this.templateName.trim(),
      imageUrl: this.templateImageUrl,
      sortOrder: 0,
      layers: this.templateDraftLayers,
    };

    this.microMajorService.createCertificateTemplate(input).subscribe({
      next: () => {
        this.message.success('证书模板已保存');
        this.templateName = '';
        this.templateImageUrl = '';
        this.templateDraftLayers = [];
        this.templateUploadProgress.set(0);
        this.loadCertificateTemplates(this.templateMicroMajorId);
      },
      error: (err) => this.message.error('保存失败: ' + (err?.error?.error?.message || err?.message || '未知错误')),
    });
  }

  deleteCertificateTemplate(template: MicroMajorCertificateTemplateDto): void {
    this.microMajorService.deleteCertificateTemplate(template.id).subscribe({
      next: () => {
        this.message.success('证书模板已删除');
        this.loadCertificateTemplates(this.templateMicroMajorId);
      },
      error: () => this.message.error('删除失败'),
    });
  }

  // ==================== 发证（选择微专业下的证书模板） ====================
  openIssueCertificateModal(item: MicroMajorEnrollmentDto): void {
    this.certificateEnrollmentId = item.id;
    this.issueEnrollment = item;
    this.issueTemplates.set([]);
    this.issueDefaults.set(undefined);
    this.issueDefaultsLoading = true;
    this.issueTemplatesLoading = true;
    // 预加载该微专业的证书模板，发证时直接选择
    this.microMajorService.getCertificateTemplates(item.microMajorId).subscribe({
      next: result => this.issueTemplates.set(result || []),
      error: () => this.message.error('证书模板加载失败'),
      complete: () => { this.issueTemplatesLoading = false; },
    });
    // 拉取自动填充默认值（姓名、学号、证书编号、发证时间）
    this.microMajorService.getIssueCertificateDefaults(item.id).subscribe({
      next: d => this.issueDefaults.set(d),
      error: () => this.issueDefaults.set(undefined),
      complete: () => { this.issueDefaultsLoading = false; },
    });
    this.certificateModalVisible = true;
  }

  closeCertificateModal(): void {
    this.certificateModalVisible = false;
    this.certificateEnrollmentId = '';
    this.issueEnrollment = undefined;
    this.issueDefaults.set(undefined);
    this.issueDefaultsLoading = false;
    this.issueTemplatesLoading = false;
  }

  openTemplateImagePreview(url: string): void {
    this.certificatePreviewUrl = url;
    this.certificatePreviewVisible = true;
  }

  openCertificatePreview(url: string): void {
    this.certificatePreviewUrl = url;
    this.certificatePreviewVisible = true;
  }

  onIssueConfirmed(input: IssueCertificateInput): void {
    this.microMajorService.issueCertificate(input).subscribe({
      next: () => {
        this.closeCertificateModal();
        this.message.success('证书已发放');
        // 切到全部 tab 再 reload，确保已发证的学生可见
        this.enrollmentFilter.set(null);
        this.reload();
      },
      error: (err) => {
        if (err?.status === 401) {
          this.message.error('登录已过期，请刷新页面后重新登录');
        } else {
          this.message.error('发证失败: ' + (err?.error?.error?.message || err?.message || '证书编号可能重复或模板无效'));
        }
      },
    });
  }

  approveEnrollment(enrollmentId: string): void {
    this.microMajorService.approveEnrollment(enrollmentId).subscribe({
      next: () => {
        this.message.success('报名已通过');
        this.reload();
      },
      error: () => this.message.error('操作失败'),
    });
  }

  rejectEnrollment(enrollmentId: string): void {
    this.microMajorService.rejectEnrollment(enrollmentId).subscribe({
      next: () => {
        this.message.success('报名已拒绝');
        this.reload();
      },
      error: () => this.message.error('操作失败'),
    });
  }

  markAsCompleted(enrollmentId: string): void {
    this.microMajorService.markAsCompleted(enrollmentId).subscribe({
      next: () => {
        this.message.success('已结业');
        this.reload();
      },
      error: (err) => this.message.error('结业失败: ' + (err?.error?.error?.message || err?.message || '未知错误')),
    });
  }

  getStatusLabel(status: MicroMajorStatus): string {
    const labels: Record<number, string> = {
      [MicroMajorStatus.Draft]: '草稿',
      [MicroMajorStatus.Published]: '已发布',
      [MicroMajorStatus.Archived]: '已归档',
    };
    return labels[status] || '未知';
  }

  getEnrollmentStatusLabel(status: MicroMajorEnrollmentStatus): string {
    const labels: Record<number, string> = {
      [MicroMajorEnrollmentStatus.Pending]: '待审批',
      [MicroMajorEnrollmentStatus.Enrolled]: '已通过',
      [MicroMajorEnrollmentStatus.InProgress]: '学习中',
      [MicroMajorEnrollmentStatus.Completed]: '已完成',
      [MicroMajorEnrollmentStatus.Certified]: '已发证',
      [MicroMajorEnrollmentStatus.Cancelled]: '已取消',
    };
    return labels[status] || '未知';
  }
}
