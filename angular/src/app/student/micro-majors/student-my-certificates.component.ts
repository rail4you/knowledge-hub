import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MicroMajorService } from '../../proxy/micro-majors/micro-major.service';
import type {
  CertificateTemplateLayerDto,
  MicroMajorCertificateDto,
} from '../../proxy/micro-majors/dtos/models';
import { MicroMajorCertificateStatus } from '../../proxy/micro-majors/enums/micro-major-certificate-status.enum';
import { AuthErrorService } from '../../core/auth/auth-error.service';
import { buildDownloadFileName } from '../../shared/download/download-file.util';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import {
  canvasToBlob,
  drawCertificate,
  formatChineseDate,
  loadCertificateImage,
  type CertificateRenderData,
} from '../../admin/micro-majors/certificate-canvas.util';

/** 证书图层结构（取自画布工具的签名，避免直接依赖手写的 micro-major.service） */
type CanvasLayer = Parameters<typeof drawCertificate>[2];

/**
 * 学生端 · 我的证书（原型 #certificate 的证书卡形态）。
 *
 * 数据来源：`GET /api/app/micro-major/my-certificates`（真实接口）。
 *
 * 证书呈现有两种方式：
 *  1. **动态合成（优先）**：用发证时所选模板的底图 + 占位符图层 + 本证书真实数据，
 *     在 canvas 上实时合成。模板事后新增/调整占位符立即生效。
 *  2. **烘焙图兜底**：模板已删除或历史证书无模板关联时，直接展示发证时合成的
 *     CertificateImageUrl。
 *
 * 诚实说明：本证书是平台内部的学习成果记录，**不具备对外认证效力**，
 * 平台没有提供证书验真 / 第三方查询服务，页面因此不展示「防伪」「可验真」等说法，
 * 也不展示 verifyCode（无可用查询入口，展示只会误导）。
 */
@Component({
  selector: 'app-student-my-certificates',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    RouterLink,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
  ],
  templateUrl: './student-my-certificates.component.html',
  styleUrls: ['./student-my-certificates.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyCertificatesComponent implements OnInit {
  private readonly microMajorService = inject(MicroMajorService);
  private readonly message = inject(NzMessageService);
  private readonly authErrorService = inject(AuthErrorService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
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
    () => this.certificates().filter(c => c.status !== MicroMajorCertificateStatus.Revoked).length,
  );

  /** 已撤销数量 */
  readonly revokedCount = computed(
    () => this.certificates().filter(c => c.status === MicroMajorCertificateStatus.Revoked).length,
  );

  ngOnInit(): void {
    this.load();
  }

  /** 加载我的证书 */
  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.microMajorService.getMyCertificates().subscribe({
      next: list => {
        this.certificates.set(list || []);
        this.loading.set(false);
      },
      error: (err: any) => {
        this.loading.set(false);
        if (err?.status === 401 || err?.status === 403) {
          this.authErrorService.setAuthError(
            err.status,
            err.error?.error?.message || err.error?.message || '您未获得授权！',
          );
          return;
        }
        this.loadFailed.set(true);
      },
    });
  }

  /** 重新加载（错误态重试） */
  reload(): void {
    this.load();
  }

  /** 证书状态文案 */
  statusLabel(c: MicroMajorCertificateDto): string {
    return c.status === MicroMajorCertificateStatus.Revoked ? '已撤销' : '有效';
  }

  /** 证书状态色调 */
  statusTone(c: MicroMajorCertificateDto): string {
    return c.status === MicroMajorCertificateStatus.Revoked ? 'revoked' : 'active';
  }

  /** 是否已撤销 */
  isRevoked(c: MicroMajorCertificateDto): boolean {
    return c.status === MicroMajorCertificateStatus.Revoked;
  }

  /** 是否有可预览的证书图（历史数据可能只有记录没有图片） */
  hasImage(c: MicroMajorCertificateDto): boolean {
    return !!c.certificateImageUrl;
  }

  /**
   * 是否可动态合成：模板底图 + 至少一个占位符图层。
   * 模板被删除或历史证书未关联模板时返回 false，由烘焙图兜底。
   */
  canRenderDynamic(c: MicroMajorCertificateDto | null): boolean {
    return !!c?.templateImageUrl && (c.layers?.length ?? 0) > 0;
  }

  /** 是否可查看（动态合成或烘焙图任一可用） */
  canPreview(c: MicroMajorCertificateDto): boolean {
    return this.canRenderDynamic(c) || this.hasImage(c);
  }

  /** 把后端图层 DTO 转为 canvas 工具所需的结构（补全可选字段默认值） */
  private toLayers(dtos: CertificateTemplateLayerDto[] | undefined): CanvasLayer {
    return (dtos ?? []).map(d => ({
      id: d.id ?? '',
      fieldType: d.fieldType ?? '',
      label: d.label ?? '',
      customFieldName: d.customFieldName ?? undefined,
      x: d.x ?? 50,
      y: d.y ?? 50,
      fontSize: d.fontSize ?? 40,
      color: d.color || 'var(--kh-ink)',
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

  /** 打开证书预览 */
  openPreview(c: MicroMajorCertificateDto): void {
    if (!this.canPreview(c)) {
      this.message.warning('该证书暂无可预览的图片，请联系发证老师');
      return;
    }
    this.previewItem.set(c);
  }

  /** 关闭预览 */
  closePreview(): void {
    this.previewItem.set(null);
  }

  /**
   * 下载证书：动态合成时导出 canvas 为 PNG（含占位符文字）；
   * 卡片上的直接下载没有预览画布，用临时画布渲染；
   * 两者都不可用时回退到打开发证时保存的原图。
   */
  async downloadCertificate(c: MicroMajorCertificateDto): Promise<void> {
    const canvas = this.previewCanvas()?.nativeElement;
    if (this.canRenderDynamic(c)) {
      if (canvas && canvas.width > 0) {
        const ok = await this.exportCanvas(canvas, c);
        if (ok) return;
      } else {
        const ok = await this.exportCanvas(document.createElement('canvas'), c);
        if (ok) return;
      }
    }
    if (c.certificateImageUrl) {
      window.open(c.certificateImageUrl, '_blank');
    }
  }

  /** 渲染画布并触发下载 */
  private async exportCanvas(canvas: HTMLCanvasElement, c: MicroMajorCertificateDto): Promise<boolean> {
    try {
      if (canvas.width === 0 || canvas.height === 0) {
        // 模板图为 OSS 跨域图片，必须经图片代理加载，否则 canvas 被污染、无法导出下载
        const image = await loadCertificateImage(c.templateImageUrl!);
        drawCertificate(canvas, image, this.toLayers(c.layers), this.buildRenderData(c), false);
      }
      const blob = await canvasToBlob(canvas);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = this.certificateFileName(c);
      link.click();
      URL.revokeObjectURL(url);
      return true;
    } catch {
      this.message.error('证书导出失败，已改为打开原图');
      return false;
    }
  }

  /** 下载文件名 */
  certificateFileName(c: MicroMajorCertificateDto | null): string {
    if (!c) return 'certificate.png';
    const title = (c.microMajorTitle || '微专业').trim();
    const no = (c.certificateNo || '').trim();
    const base = no ? `${title}证书-${no}` : `${title}证书`;
    return buildDownloadFileName(base, null, 'png') || 'certificate.png';
  }

  /** 前往微专业中心 */
  goToCenter(): void {
    this.router.navigate(['/student/micro-majors']);
  }
}
