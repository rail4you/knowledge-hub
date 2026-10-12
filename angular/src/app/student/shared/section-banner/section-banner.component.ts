import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * 学习空间统一顶部横幅（替代每个页面重复的 section-banner 样式）。
 *
 * 用法：
 *   <app-section-banner
 *     [eyebrow]="为成长而精选"
 *     title="课程中心"
 *     description="汇聚专业精品课程..."
 *   />
 */
@Component({
  selector: 'app-section-banner',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './section-banner.component.html',
  styleUrls: ['./section-banner.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SectionBannerComponent {
  /** 顶部小标（中文，2-8 字简短标签，可选） */
  readonly eyebrow = input<string>('');
  /** 主标题（中文） */
  readonly title = input.required<string>();
  /** 副描述（中文，1-2 句） */
  readonly description = input<string>('');
}
