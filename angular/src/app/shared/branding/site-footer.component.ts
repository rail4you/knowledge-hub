import { Component, ChangeDetectionStrategy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BrandingService } from './branding.service';

/**
 * 三端统一页脚：只显示一段文本，无链接。
 * 管理端（LeptonX Footer 替换）/ 学生端 / 首页共用。
 */
@Component({
  selector: 'app-site-footer',
  standalone: true,
  imports: [CommonModule],
  template: `<footer class="sf">{{ branding.footerText() }}</footer>`,
  styles: [
    `
      :host {
        display: block;
      }
      .sf {
        text-align: center;
        padding: 20px 16px;
        color: #8c8c8c;
        font-size: 13px;
        line-height: 1.6;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteFooterComponent implements OnInit {
  readonly branding = inject(BrandingService);

  ngOnInit(): void {
    this.branding.ensureLoaded();
  }
}
