import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MicroMajorService } from '../../proxy/micro-majors/micro-major.service';
import type { MicroMajorCertificateDto } from '../../proxy/micro-majors/dtos/models';
import { MicroMajorCertificateStatus } from '../../proxy/micro-majors/enums/micro-major-certificate-status.enum';
import { AuthErrorService } from '../../core/auth/auth-error.service';
import { buildDownloadFileName } from '../../shared/download/download-file.util';

/**
 * 学生端「我的证书」。
 *
 * 微专业结业后由教师发证，证书图片在发证时由管理端画布合成并上传。
 * 这里集中展示当前学生的全部微专业证书（含证书编号、验证码、发证日期、有效期、状态），
 * 支持预览与下载，直接回答「发的证在哪看」。
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
    if (!this.hasImage(c)) {
      this.message.warning('该证书暂无可预览的图片，请联系发证老师');
      return;
    }
    this.previewItem.set(c);
  }

  closePreview(): void {
    this.previewItem.set(null);
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
