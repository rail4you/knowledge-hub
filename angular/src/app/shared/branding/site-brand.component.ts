import { Component, ChangeDetectionStrategy, OnInit, inject, input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BrandingService } from './branding.service';

/**
 * 三端统一品牌 lockup：Logo（图片 URL，空则用标题首字文字 Logo）
 * + 应用标题 + 副标题。自带基础样式，不依赖各页面的 .brand 样式。
 */
@Component({
  selector: 'app-site-brand',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="sb">
      @if (branding.logoUrl()) {
        <img
          class="sb__img"
          [src]="branding.logoUrl()"
          [alt]="branding.appTitle()"
          [style.width.px]="markSize()"
          [style.height.px]="markSize()"
        />
      } @else {
        <svg
          class="sb__mark"
          [attr.width]="markSize()"
          [attr.height]="markSize()"
          viewBox="0 0 30 30"
          fill="none"
          aria-hidden="true"
        >
          <rect width="30" height="30" rx="8" fill="#13233A" stroke="#1890FF" stroke-width="1.2" />
          <text
            x="15"
            y="21"
            text-anchor="middle"
            font-size="15"
            font-weight="700"
            fill="#7EC8FF"
            font-family="PingFang SC, Microsoft YaHei, sans-serif"
          >{{ brandInitial() }}</text>
        </svg>
      }
      <span class="sb__name">{{ branding.appTitle() }}</span>
      <span class="sb__sub">{{ sub() || branding.appSubtitle() }}</span>
    </span>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
      }
      .sb {
        display: inline-flex;
        align-items: center;
        gap: 8px;
      }
      .sb__img {
        border-radius: 8px;
        object-fit: contain;
      }
      .sb__name {
        font-size: 16px;
        font-weight: 700;
        color: inherit;
        white-space: nowrap;
      }
      .sb__sub {
        font-size: 12px;
        opacity: 0.65;
        white-space: nowrap;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteBrandComponent implements OnInit {
  readonly branding = inject(BrandingService);

  /** 副标题覆盖（如学生端传“学生中心”）；不传则用全局副标题 */
  readonly sub = input<string>('');
  /** Logo 图标尺寸 px */
  readonly markSize = input<number>(26);

  /** 文字 Logo 取标题首字（默认为“易”） */
  readonly brandInitial = computed(() => this.branding.appTitle().trim().charAt(0) || '易');

  ngOnInit(): void {
    this.branding.ensureLoaded();
  }
}
