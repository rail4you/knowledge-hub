import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  ElementRef,
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
import { OssUploadService } from '../../shared/oss-upload.service';
import {
  CertificateLayer,
  IssueCertificateDefaultsDto,
  IssueCertificateInput,
  MicroMajorCertificateTemplateDto,
  MicroMajorEnrollmentDto,
} from '../../micro-majors/micro-major.service';
import {
  CertificateRenderData,
  canvasToBlob,
  drawCertificate,
  formatChineseDate as formatCnDate,
  loadCertificateImage,
  toDateInputValue,
} from './certificate-canvas.util';

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
  @Input() enrollment!: MicroMajorEnrollmentDto;
  @Input() templates: MicroMajorCertificateTemplateDto[] = [];
  @Input() defaults!: IssueCertificateDefaultsDto;

  @Output() readonly cancel = new EventEmitter<void>();
  @Output() readonly confirmed = new EventEmitter<IssueCertificateInput>();

  @ViewChild('canvas') canvasRef!: ElementRef<HTMLCanvasElement>;

  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);

  selectedTemplateId = '';
  readonly issueLoading = signal(false);
  readonly imageLoading = signal(false);

  // 表单字段
  studentName = '';
  studentNo = '';
  advisor = '';
  issueDate = '';
  certificateNo = '';
  validUntil = '';

  private imageElement?: HTMLImageElement;

  get selectedTemplate(): MicroMajorCertificateTemplateDto | undefined {
    return this.templates.find(t => t.id === this.selectedTemplateId);
  }

  get layers(): CertificateLayer[] {
    return this.selectedTemplate?.layers ?? [];
  }

  ngOnChanges(): void {
    this.initializeFields();
    this.reloadPreview();
  }

  ngOnDestroy(): void {
    // no-op
  }

  private initializeFields(): void {
    this.studentName = this.defaults?.studentName ?? this.enrollment?.studentName ?? '';
    this.studentNo = this.defaults?.studentNo ?? '';
    this.issueDate = this.defaults?.issueDate ? toDateInputValue(this.defaults.issueDate) : toDateInputValue(new Date());
    this.certificateNo = this.defaults?.suggestedCertificateNo ?? '';
    this.advisor = '';
    this.validUntil = '';
  }

  /** 模板中调用：将日期格式化为中文（2026年8月30日） */
  formatChineseDate(date: string): string {
    return formatCnDate(date);
  }

  onTemplateChange(): void {
    this.reloadPreview();
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
      studentNo: this.studentNo,
      advisor: this.advisor,
      issueDate: this.issueDate ? formatCnDate(new Date(this.issueDate)) : '',
      certificateNo: this.certificateNo,
      validUntil: this.validUntil ? formatCnDate(new Date(this.validUntil)) : '',
      microMajorTitle: this.enrollment.microMajorTitle ?? this.defaults?.microMajorTitle ?? '',
      custom: undefined,
    };
  }

  private async reloadPreview(): Promise<void> {
    const tpl = this.selectedTemplate;
    if (!tpl?.imageUrl) {
      this.imageElement = undefined;
      return;
    }
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
    if (!this.enrollment?.id) return;
    if (!this.certificateNo.trim()) {
      this.message.warning('请填写证书编号');
      return;
    }
    if (!this.issueDate) {
      this.message.warning('请选择发证时间');
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
      const input: IssueCertificateInput = {
        enrollmentId: this.enrollment.id,
        certificateTemplateId: this.selectedTemplate.id,
        studentNo: this.studentNo.trim() || undefined,
        advisor: this.advisor.trim() || undefined,
        issueDate: this.issueDate ? new Date(this.issueDate).toISOString() : undefined,
        validUntil: this.validUntil ? new Date(this.validUntil).toISOString() : undefined,
        certificateNo: this.certificateNo.trim(),
        compositeImageUrl: uploaded.url,
      };
      this.confirmed.emit(input);
    } catch (err: any) {
      this.message.error('合成证书图片失败: ' + (err?.error?.error?.message || err?.message || '未知错误'));
    } finally {
      this.issueLoading.set(false);
    }
  }
}
