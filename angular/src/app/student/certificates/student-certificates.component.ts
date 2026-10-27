import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CertificateService } from '../../proxy/certificates/certificate.service';
import type {
  CertificateApplicationDto,
  CertificateApplyEligibilityDto,
  CertificateDto,
} from '../../proxy/certificates/dtos/models';
import { CertificateApplicationStatus } from '../../proxy/certificates/certificate-application-status.enum';
import { LearningService } from '../../proxy/learning/learning.service';
import { ResourceLibraryLearnerService } from '../../proxy/resource-libraries/resource-library-learner.service';
import {
  buildCertificateFileName,
  downloadCertificateImage,
} from '../../shared/certificate/certificate-download.util';

/**
 * 学生端「课程证书」。
 *
 * <para>
 * PRD 第 9 节：其他学员完成学习并通过考核后，在个人端主动申请结业证书；
 * 申请提交后由平台管理员统一审核，审核通过后平台发放电子证书。
 * </para>
 */
@Component({
  selector: 'app-student-certificates',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzIconModule,
    NzModalModule,
    NzSpinModule,
    NzTableModule,
    NzTabsModule,
    NzTagModule,
  ],
  templateUrl: './student-certificates.component.html',
  styleUrls: ['./student-certificates.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentCertificatesComponent implements OnInit {
  private readonly certificateService = inject(CertificateService);
  private readonly learningService = inject(LearningService);
  private readonly learnerService = inject(ResourceLibraryLearnerService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly certificates = signal<CertificateDto[]>([]);
  readonly applications = signal<CertificateApplicationDto[]>([]);

  /** 我的课程（用于判断可申请课程证书的课程） */
  readonly myCourses = signal<{ id: string; title: string; status?: number | null }[]>([]);
  readonly eligibility = signal<Record<string, CertificateApplyEligibilityDto>>({});

  readonly applyModalVisible = signal(false);
  readonly applying = signal(false);
  /** 正在下载的证书 Id（用于按钮 loading）。 */
  readonly downloadingId = signal<string | null>(null);
  applyTarget: { id: string; title: string } | null = null;
  applyReason = '';

  readonly statusLabels: Record<number, string> = {
    0: '待审核',
    1: '已发证',
    2: '已驳回',
    3: '已撤回',
  };

  readonly completedCount = computed(() => this.applications().filter(a => a.status === CertificateApplicationStatus.Approved).length);

  /** 待审核的申请数（页头统计）。 */
  readonly pendingCount = computed(() => this.applications().filter(a => a.status === CertificateApplicationStatus.Pending).length);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.certificateService.getMyCertificates().subscribe({
      next: list => {
        this.certificates.set(list || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载证书失败');
      },
    });

    this.certificateService.getMyApplications().subscribe({
      next: list => this.applications.set(list || []),
      error: () => undefined,
    });

    this.learningService.getMyCourses().subscribe({
      next: (res: any) => {
        const items = (res?.items ?? res ?? []) as any[];
        this.myCourses.set(
          items.map(c => ({ id: c.courseId ?? c.id, title: c.courseTitle ?? c.title, status: c.status })),
        );
        this.checkEligibility(items.map(c => c.courseId ?? c.id));
      },
      error: () => undefined,
    });
  }

  /** 批量检查每门课程的课程证书申请资格 */
  private checkEligibility(courseIds: string[]): void {
    const unique = Array.from(new Set(courseIds.filter(Boolean)));
    unique.forEach(id => {
      this.certificateService.getApplyEligibility(id).subscribe({
        next: res => {
          if (res) {
            this.eligibility.update(map => ({ ...map, [id]: res }));
          }
        },
        error: () => undefined,
      });
    });
  }

  /** 可申请的课程（资格通过） */
  applyableCourses(): { id: string; title: string }[] {
    return this.myCourses().filter(c => {
      const e = this.eligibility()[c.id];
      return e?.canApply === true;
    });
  }

  openApply(course: { id: string; title: string }): void {
    const e = this.eligibility()[course.id];
    this.applyTarget = course;
    this.applyReason = e?.reason ?? '';
    this.applyModalVisible.set(true);
  }

  confirmApply(): void {
    if (!this.applyTarget) return;
    this.applying.set(true);
    this.certificateService.apply({ courseId: this.applyTarget.id }).subscribe({
      next: () => {
        this.applying.set(false);
        this.applyModalVisible.set(false);
        this.message.success('申请已提交，等待平台管理员审核');
        this.reload();
      },
      error: (err: any) => {
        this.applying.set(false);
        this.message.error(err?.error?.error?.message || '申请失败');
      },
    });
  }

  /**
   * 下载证书图片。
   *
   * <para>证书图存在 OSS（跨域），直接 `a[download]` 会被浏览器当作打开图片，
   * 因此走「fetch → blob → 下载」；直连失败时自动改走站内图片代理。</para>
   */
  async download(cert: CertificateDto): Promise<void> {
    if (!cert.imageUrl) {
      this.message.warning('该证书暂无可下载的图片，请联系发证机构');
      return;
    }

    this.downloadingId.set(cert.id);
    const fileName = buildCertificateFileName(
      (cert.courseTitle || '课程证书').trim(),
      cert.certificateNo,
    );
    const ok = await downloadCertificateImage(cert.imageUrl, fileName);
    this.downloadingId.set(null);

    if (!ok) {
      // 兜底：直接在新窗口打开原图，用户可自行保存
      window.open(cert.imageUrl, '_blank');
      this.message.warning('证书图片下载失败，已为你打开原图，请长按或右键另存');
      return;
    }

    this.message.success('证书已开始下载');
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }
}
