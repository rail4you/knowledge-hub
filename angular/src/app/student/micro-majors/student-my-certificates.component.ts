import { ChangeDetectionStrategy, Component, ElementRef, OnInit, computed, effect, inject, signal, viewChild } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MicroMajorService } from '../../proxy/micro-majors/micro-major.service';
import type { CertificateTemplateLayerDto, MicroMajorCertificateDto } from '../../proxy/micro-majors/dtos/models';
import type { CertificateLayer } from '../../micro-majors/micro-major.service';
import { MicroMajorCertificateStatus } from '../../proxy/micro-majors/enums/micro-major-certificate-status.enum';
import { AuthErrorService } from '../../core/auth/auth-error.service';
import { buildDownloadFileName } from '../../shared/download/download-file.util';
import {
  canvasToBlob,
  drawCertificate,
  formatChineseDate,
  loadCertificateImage,
  type CertificateRenderData,
} from '../../admin/micro-majors/certificate-canvas.util';

/**
 * 学生端「我的证书」。
 *
 * 微专业结业后由教师发证。证书展示有两种方式：
 * 1. **动态合成（优先）**：用发证时所选模板的底图 + 占位符图层 + 本证书真实数据，
 *    在 canvas 上实时合成。模板事后新增/调整占位符（姓名、微专业名称等）立即生效。
 * 2. **烘焙图兑底**：模板已删除或历史证书无模板关联时，直接展示发证时合成的
 *    CertificateImageUrl。
 */
@Component({
  selector: 'app-student-my-certificates',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, NzIconModule, NzSpinModule],
  templateUrl: './student-my-certificates.component.html',
  styleUrls: ['./student-my-certificates.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyCertificatesComponent implements OnInit {
  private readonly microMajorService = inject(MicroMajorService);
  private readonly message = inject(NzMessageService);
  private readonly authErrorService = inject(AuthErrorService);

  readonly loading = signal(false);
  readonly certificates = signal<MicroMajorCertificateDto[]>([]);
  /** 预览弹窗：当前选中的证书 */
  readonly previewItem = signal<MicroMajorCertificateDto | null>(null);
  /** 预览 canvas（动态渲染时使用） */
  private readonly previewCanvas = viewChild<ElementRef<HTMLCanvasElement>>('previewCanvas');

  constructor() {
    // 弹窗打开且 canvas 已进入 DOM 后合成证书，避免 @if 首帧取不到元素。
    effect(() => {
      const item = this.previewItem();
      const canvas = this.previewCanvas()?.nativeElement;
      if (!item || !canvas || !this.canRenderDynamic(item)) {
        return;
      }
      void this.renderPreview(item, canvas);
    });
  }

  /** 有效证书数量（撤销的不计入） */
  readonly activeCount = computed(
    () => this.certificates().filter((c) => c.status !== MicroMajorCertificateStatus.Revoked).length,
  );

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.microMajorService.getMyCertificates().subscribe({
      next: (list) => {
        this.certificates.set(list || []);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err?.status === 401 || err?.status === 403) {
          this.authErrorService.setAuthError(
            err.status,
            err.error?.error?.message || err.error?.message || '您未获得授权！',
          );
        } else {
          this.message.error('证书加载失败');
        }
      },
    });
  }

  isRevoked(c: MicroMajorCertificateDto): boolean {
    return c.status === MicroMajorCertificateStatus.Revoked;
  }

  /** 是否有可预览的证书图（历史数据可能只有记录没有图片） */
  hasImage(c: MicroMajorCertificateDto): boolean {
    return !!c.certificateImageUrl;
  }

  /**
   * 是否可动态合成：模板底图 + 至少一个占位符图层。
   * 模板被删除或历史证书未关联模板时返回 false，由烘焙图兑底。
   */
  canRenderDynamic(c: MicroMajorCertificateDto | null): boolean {
    return !!c?.templateImageUrl && (c.layers?.length ?? 0) > 0;
  }

  /** 是否可查看（动态合成或烘焙图任一可用） */
  canPreview(c: MicroMajorCertificateDto): boolean {
    return this.canRenderDynamic(c) || this.hasImage(c);
  }

  /** 把后端图层 DTO 转为 canvas 工具所需的结构（补全可选字段默认值） */
  private toLayers(dtos: CertificateTemplateLayerDto[] | undefined): CertificateLayer[] {
    return (dtos ?? []).map((d) => ({
      id: d.id ?? '',
      fieldType: d.fieldType ?? '',
      label: d.label ?? '',
      customFieldName: d.customFieldName ?? undefined,
      x: d.x ?? 50,
      y: d.y ?? 50,
      fontSize: d.fontSize ?? 40,
      color: d.color ?? '#183444',
      fontWeight: d.fontWeight ?? 400,
      center: d.center ?? false,
      fontFamily: d.fontFamily ?? undefined,
    }));
  }

  /** 组装占位符的回填数据 */
  private buildRenderData(c: MicroMajorCertificateDto): CertificateRenderData {
    return {
      studentName: c.studentName ?? '',
      studentNo: c.studentNo ?? '',
      advisor: c.advisor ?? '',
      issueDate: c.issueDate ? formatChineseDate(c.issueDate) : '',
      certificateNo: c.certificateNo ?? '',
      validUntil: c.validUntil ? formatChineseDate(c.validUntil) : '',
      microMajorTitle: c.microMajorTitle ?? '',
    };
  }

  private async renderPreview(c: MicroMajorCertificateDto, canvas: HTMLCanvasElement): Promise<void> {
    try {
      // 模板图为 OSS 跨域图片，必须经图片代理加载，否则 canvas 被污染、无法导出下载
      const image = await loadCertificateImage(c.templateImageUrl!);
      drawCertificate(canvas, image, this.toLayers(c.layers), this.buildRenderData(c), false);
    } catch {
      this.message.error('证书渲染失败，已回退到发证时保存的图片');
      // 渲染失败时清空模板信息，模板会改走烘焙图分支
      this.previewItem.set({ ...c, templateImageUrl: null, layers: [] });
    }
  }

  /** 打印在证书上的发证日期优先，缺失时回退到发证时间 */
  issueDate(c: MicroMajorCertificateDto): Date | null {
    const raw = c.issueDate || c.issuedAt;
    if (!raw) return null;
    const d = new Date(raw);
    return Number.isNaN(d.getTime()) ? null : d;
  }

  /** 有效期是否已过 */
  isExpired(c: MicroMajorCertificateDto): boolean {
    if (!c.validUntil) return false;
    const d = new Date(c.validUntil);
    return !Number.isNaN(d.getTime()) && d.getTime() < Date.now();
  }

  openPreview(c: MicroMajorCertificateDto): void {
    if (!this.canPreview(c)) {
      this.message.warning('该证书暂无可预览的图片，请联系发证老师');
      return;
    }
    this.previewItem.set(c);
  }

  closePreview(): void {
    this.previewItem.set(null);
  }

  /**
   * 下载证书：动态合成时导出 canvas 为 PNG（含占位符文字）；
   * 否则回退到烘焙图地址。
   */
  async downloadCertificate(c: MicroMajorCertificateDto): Promise<void> {
    const canvas = this.previewCanvas()?.nativeElement;
    if (this.canRenderDynamic(c) && canvas && canvas.width > 0) {
      try {
        const blob = await canvasToBlob(canvas);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = this.certificateFileName(c);
        link.click();
        URL.revokeObjectURL(url);
        return;
      } catch {
        this.message.error('证书导出失败，已改为打开原图');
      }
    }
    if (c.certificateImageUrl) {
      window.open(c.certificateImageUrl, '_blank');
    }
  }

  certificateFileName(c: MicroMajorCertificateDto | null): string {
    if (!c) return 'certificate.png';
    const title = (c.microMajorTitle || '微专业').trim();
    const no = (c.certificateNo || '').trim();
    const base = no ? `${title}证书-${no}` : `${title}证书`;
    // 复用统一工具保证扩展名不丢（文件名里含编号便于区分多张证书）
    return buildDownloadFileName(base, null, 'png') || 'certificate.png';
  }
}
