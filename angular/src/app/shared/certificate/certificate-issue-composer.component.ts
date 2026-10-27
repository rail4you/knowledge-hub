import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { OssUploadService } from '../oss-upload.service';
import { CertificateService } from '../../proxy/certificates/certificate.service';
import type {
  ApproveCertificateInput,
  CertificateApplicationDto,
  CertificateTemplateDto,
} from '../../proxy/certificates/dtos/models';
import { CertificateLayer, CertificateRenderData } from './certificate.models';
import {
  canvasToBlob,
  drawCertificate,
  formatChineseDate as formatCnDate,
  loadCertificateImage,
  toDateInputValue,
} from './certificate-canvas.util';

/**
 * 教学资源库结业证书「审核通过并发证」弹窗（PRD 第 9 节「审核发放」）。
 *
 * <para>流程：读取申请 → 取得证书编号 / 防伪码 → 在模板上合成证书图片 →
 * 上传图片 → 提交审核通过（含证书图片 URL）。</para>
 */
@Component({
  selector: 'app-certificate-issue-composer',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
  ],
  templateUrl: './certificate-issue-composer.component.html',
  styleUrls: ['./certificate-issue-composer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertificateIssueComposerComponent implements OnChanges, OnDestroy {
  @Input() application!: CertificateApplicationDto;
  @Input() templates: CertificateTemplateDto[] = [];

  @Output() readonly cancelled = new EventEmitter<void>();
  @Output() readonly confirmed = new EventEmitter<ApproveCertificateInput>();

  @ViewChild('canvas') canvasRef!: ElementRef<HTMLCanvasElement>;

  private readonly ossUploadService = inject(OssUploadService);
  private readonly certificateService = inject(CertificateService);
  private readonly message = inject(NzMessageService);

  selectedTemplateId = '';
  readonly issueLoading = signal(false);
  readonly imageLoading = signal(false);

  // 表单字段
  studentName = '';
  courseTitle = '';
  schoolName = '';
  libraryName = '';
  issueDate = '';
  certificateNo = '';
  verifyCode = '';
  validUntil = '';
  reviewRemark = '';

  private imageElement?: HTMLImageElement;
  private layers: CertificateLayer[] = [];

  get selectedTemplate(): CertificateTemplateDto | undefined {
    return this.templates.find(t => t.id === this.selectedTemplateId);
  }

  ngOnChanges(): void {
    this.initializeFields();
    this.reloadPreview();
  }

  ngOnDestroy(): void {
    // no-op
  }

  private initializeFields(): void {
    const app = this.application;
    this.studentName = app?.applicantName ?? '';
    this.courseTitle = app?.courseTitle ?? '';
    this.schoolName = app?.schoolName ?? '';
    this.libraryName = app?.libraryName ?? '';
    this.issueDate = toDateInputValue(new Date());
    this.reviewRemark = '';

    // 默认模板
    const def = this.templates.find(t => t.isDefault) ?? this.templates[0];
    this.selectedTemplateId = def?.id ?? '';

    // 证书编号与防伪码由后端生成（前端仅用于合成图片）
    if (app?.id) {
      this.certificateService.prepare(app.id).subscribe({
        next: prepared => {
          this.certificateNo = prepared.certificateNo ?? '';
          this.verifyCode = prepared.verifyCode ?? '';
        },
        error: () => {
          this.certificateNo = '';
          this.verifyCode = '';
        },
      });
    }
  }

  onTemplateChange(): void {
    this.layers = this.parseLayers(this.selectedTemplate?.layersJson);
    this.reloadPreview();
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

  /** 字段变化时仅重绘画布，无需重新加载图片 */
  onFieldChange(): void {
    if (this.imageElement) {
      this.redraw(false);
    }
  }

  private get canvas(): HTMLCanvasElement {
    return this.canvasRef.nativeElement;
  }

  private buildRenderData(): CertificateRenderData {
    return {
      studentName: this.studentName,
      courseTitle: this.courseTitle,
      schoolName: this.schoolName,
      libraryName: this.libraryName,
      issueDate: this.issueDate ? formatCnDate(new Date(this.issueDate)) : '',
      certificateNo: this.certificateNo,
      verifyCode: this.verifyCode,
      validUntil: this.validUntil ? formatCnDate(new Date(this.validUntil)) : '',
      custom: undefined,
    };
  }

  private async reloadPreview(): Promise<void> {
    const tpl = this.selectedTemplate;
    if (!tpl?.imageUrl) {
      this.imageElement = undefined;
      return;
    }
    this.layers = this.parseLayers(tpl.layersJson);
    this.imageLoading.set(true);
    try {
      const img = await loadCertificateImage(tpl.imageUrl);
      this.imageElement = img;
      this.redraw(false);
    } catch (err: any) {
      this.imageElement = undefined;
      this.message.error(err?.message || '证书图片加载失败');
    } finally {
      this.imageLoading.set(false);
    }
  }

  private redraw(placeholder: boolean): void {
    if (!this.imageElement || !this.canvas) return;
    drawCertificate(this.canvas, this.imageElement, this.layers, this.buildRenderData(), placeholder);
  }

  async confirmIssue(): Promise<void> {
    if (!this.application?.id) return;
    if (!this.certificateNo.trim()) {
      this.message.warning('请填写证书编号');
      return;
    }
    if (!this.issueDate) {
      this.message.warning('请选择结业时间');
      return;
    }
    if (!this.selectedTemplate) {
      this.message.warning('请选择证书模板');
      return;
    }

    this.issueLoading.set(true);
    try {
      if (!this.imageElement) {
        await this.reloadPreview();
      }
      if (!this.imageElement) {
        this.message.error('证书图片加载失败，无法发证');
        return;
      }

      // 1. 用真实数据在画布上合成最终证书图片
      this.redraw(false);
      const blob = await canvasToBlob(this.canvas);
      const file = new File([blob], `certificate-${Date.now()}.png`, { type: 'image/png' });

      // 2. 上传合成图片到 OSS
      const uploaded = await new Promise<{ url: string }>((resolve, reject) => {
        this.ossUploadService.uploadImage(file).subscribe({
          next: r => resolve(r),
          error: (err: any) => reject(err),
        });
      });

      // 3. 组装发证输入
      this.confirmed.emit({
        applicationId: this.application.id,
        templateId: this.selectedTemplate.id,
        certificateNo: this.certificateNo.trim(),
        verifyCode: this.verifyCode.trim() || undefined,
        certificateImageUrl: uploaded.url,
        issueDate: this.issueDate ? new Date(this.issueDate).toISOString() : undefined,
        validUntil: this.validUntil ? new Date(this.validUntil).toISOString() : undefined,
        reviewRemark: this.reviewRemark.trim() || undefined,
      });
    } catch (err: any) {
      this.message.error('合成证书图片失败: ' + (err?.error?.error?.message || err?.message || '未知错误'));
    } finally {
      this.issueLoading.set(false);
    }
  }
}
