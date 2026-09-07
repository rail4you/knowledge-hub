import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NewsArticleDto } from '../../../news/news.service';

export interface HeadlineHeroStat {
  label: string;
  value: number;
  suffix?: string;
  icon?: string;
  color?: string;
}

@Component({
  selector: 'app-headline-hero',
  standalone: true,
  imports: [CommonModule, DatePipe, DecimalPipe, NzIconModule],
  templateUrl: './headline-hero.component.html',
  styleUrls: ['./headline-hero.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeadlineHeroComponent {
  /** 页面标题（无头条时显示） */
  readonly title = input('资讯中心');
  /** 页面描述（无头条时显示） */
  readonly description = input('');
  /** 头条文章列表，取第 0 个作为主推 */
  readonly headlines = input<NewsArticleDto[]>([]);
  /** 右侧数据总览 */
  readonly stats = input<HeadlineHeroStat[]>([]);

  /** 用户点击进入阅读 */
  readonly articleClick = output<string>();

  /**
   * 本地默认背景图（已下载到 assets/images/，随构建打包）
   * 高分辨率报刊实景图 — 左侧稍暗、右上较亮，利于文字对比与视觉层次
   */
  readonly fallbackImage = 'assets/images/news-hero-bg_001.jpg';

  /** 当前展示的头条文章 */
  readonly headline = computed<NewsArticleDto | null>(() => this.headlines()[0] ?? null);
  readonly hasHeadline = computed(() => !!this.headline());

  /** 头条封面图 URL（若存在） */
  readonly coverUrl = computed<string | null>(() => {
    const h = this.headline();
    if (!h?.coverImageUrl) return null;
    const url = h.coverImageUrl.trim();
    return url.length > 0 ? url : null;
  });

  /**
   * 封面图加载状态：
   * - 'unknown' 初始 / 切换 URL 时
   * - 'ok'      加载成功
   * - 'fail'    加载失败（404、CORS 等）
   */
  readonly coverState = signal<'unknown' | 'ok' | 'fail'>('unknown');

  constructor() {
    // 当 coverUrl 变化时，重置状态（让隐藏 img 重新触发 load/error）
    effect(() => {
      this.coverUrl(); // 依赖追踪
      this.coverState.set('unknown');
    });
  }

  /**
   * 最终用于 background-image 的 URL：
   * - 头条无封面图 → 本地 fallback
   * - 头条有封面图且加载成功 → 使用封面
   * - 头条有封面图但加载失败 → 回退到本地 fallback
   */
  readonly backgroundImage = computed<string>(() => {
    const cover = this.coverUrl();
    if (cover && this.coverState() === 'ok') {
      return `url("${cover}")`;
    }
    return `url("${this.fallbackImage}")`;
  });

  /** 隐藏 img 加载成功 */
  onCoverLoad(): void {
    this.coverState.set('ok');
  }

  /** 隐藏 img 加载失败 */
  onCoverError(): void {
    this.coverState.set('fail');
  }
}