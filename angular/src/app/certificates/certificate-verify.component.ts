import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { CertificateService } from '../proxy/certificates/certificate.service';
import type { CertificateVerifyDto } from '../proxy/certificates/dtos/models';

/**
 * 结业证书防伪核验页（公开访问）。
 *
 * <para>PRD 第 9 节证书模板含「防伪二维码」，扫码后进入本页输入证书编号 / 防伪码即可核验真伪。</para>
 */
@Component({
  selector: 'app-certificate-verify',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzIconModule,
    NzInputModule,
    NzSpinModule,
  ],
  templateUrl: './certificate-verify.component.html',
  styleUrls: ['./certificate-verify.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertificateVerifyComponent implements OnInit {
  private readonly certificateService = inject(CertificateService);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly result = signal<CertificateVerifyDto | null>(null);
  code = '';

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    if (code) {
      this.code = code;
      this.verify();
    }
  }

  verify(): void {
    if (!this.code?.trim()) {
      this.result.set({ isValid: false, message: '请输入证书编号或防伪码。' });
      return;
    }
    this.loading.set(true);
    this.certificateService.verify(this.code.trim()).subscribe({
      next: res => {
        this.result.set(res);
        this.loading.set(false);
      },
      error: () => {
        this.result.set({ isValid: false, message: '核验失败，请稍后重试。' });
        this.loading.set(false);
      },
    });
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }
}
