import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NewsArticleDto, NewsCategoryDto, NewsService } from '../../news/news.service';
import { HeadlineHeroComponent } from '../shared/headline-hero/headline-hero.component';

interface StatItem {
  label: string;
  value: number;
  suffix: string;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-student-news',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    NzIconModule,
    NzSpinModule,
    NzDividerModule,
    HeadlineHeroComponent,
  ],
  templateUrl: './student-news.component.html',
  styleUrls: ['./student-news.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentNewsComponent implements OnInit {
  private readonly newsService = inject(NewsService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly articles = signal<NewsArticleDto[]>([]);
  readonly totalCount = signal(0);
  readonly hotArticles = signal<NewsArticleDto[]>([]);
  /** 头条文章列表（第 0 个作为主推，其他作为右侧次条） */
  readonly headlineArticles = signal<NewsArticleDto[]>([]);
  readonly categories = signal<NewsCategoryDto[]>([]);
  readonly filter = signal('');
  readonly categoryId = signal<string | null>(null);
  /** 文章属性筛选：all 全部，top 头条，hot 热门，normal 一般文章 */
  readonly attrFilter = signal<'all' | 'top' | 'hot' | 'normal'>('all');
  /** 发布时间筛选：all 全部，week 本周，month 本月，older 更早 */
  readonly timeFilter = signal<'all' | 'week' | 'month' | 'older'>('all');

  // 数据统计（从实际数据计算）
  readonly stats = computed<StatItem[]>(() => {
    const all = this.articles();
    const hot = this.hotArticles();
    const totalViews = all.reduce((sum, a) => sum + (a.viewCount || 0), 0);
    const totalLikes = all.reduce((sum, a) => sum + (a.likeCount || 0), 0);
    return [
      { label: '资讯总数', value: all.length, suffix: '篇', icon: 'file-text', color: '#1e6ce8' },
      { label: '热门资讯', value: hot.length, suffix: '篇', icon: 'fire', color: '#f59e0b' },
      { label: '总阅读量', value: totalViews, suffix: totalViews >= 10000 ? '万次' : '次', icon: 'eye', color: '#10b981' },
      { label: '总点赞量', value: totalLikes, suffix: totalLikes >= 10000 ? '万次' : '次', icon: 'like', color: '#06b6d4' },
    ];
  });

  /** 工具栏标题：时间 + 属性 + 分类组合 */
  readonly toolbarLabel = computed(() => {
    const parts: string[] = [];
    const timeLabel = this.timeFilterLabel();
    if (timeLabel) parts.push(timeLabel);
    const attrLabel = this.attrFilterLabel();
    if (attrLabel) parts.push(attrLabel);
    const cat = this.findCategoryName(this.categoryId());
    if (cat) parts.push(cat);
    return parts.length > 0 ? parts.join(' · ') : '资讯列表';
  });

  /** 当前时间筛选的中文名（全部返回空） */
  timeFilterLabel(): string {
    switch (this.timeFilter()) {
      case 'week': return '本周';
      case 'month': return '本月';
      case 'older': return '更早';
      default: return '';
    }
  }

  /** 当前属性筛选的中文名（全部返回空） */
  attrFilterLabel(): string {
    switch (this.attrFilter()) {
      case 'top': return '头条';
      case 'hot': return '热门';
      case 'normal': return '一般文章';
      default: return '';
    }
  }

  ngOnInit(): void {
    this.loadCategories();
    this.loadArticles();
    this.loadHotArticles();
    this.loadHeadlineArticles();
  }

  loadCategories(): void {
    this.newsService.getCategoryTree().subscribe({
      next: categories => {
        this.categories.set(categories || []);
      },
    });
  }

  loadArticles(): void {
    this.loading.set(true);
    const attr = this.attrFilter();
    const time = this.timeRange();
    this.newsService.getPublishedArticles({
      filter: this.filter() || undefined,
      categoryId: this.categoryId() || undefined,
      isTop: attr === 'top' ? true : attr === 'normal' ? false : undefined,
      isHot: attr === 'hot' ? true : attr === 'normal' ? false : undefined,
      publishedAfter: time.after,
      publishedBefore: time.before,
      skipCount: 0,
      maxResultCount: 30,
    }).subscribe({
      next: result => {
        this.articles.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('资讯加载失败');
      },
    });
  }

  loadHotArticles(): void {
    this.newsService.getHotArticles().subscribe({
      next: items => this.hotArticles.set(items || []),
    });
  }

  /** 拉取头条文章（isTop=true，按发布时间倒序，取前 4 篇） */
  loadHeadlineArticles(): void {
    this.newsService.getPublishedArticles({
      isTop: true,
      skipCount: 0,
      maxResultCount: 4,
    }).subscribe({
      next: result => this.headlineArticles.set(result.items || []),
      error: () => this.headlineArticles.set([]),
    });
  }

  openArticle(id: string): void {
    this.router.navigate(['/student/news', id]);
  }

  selectCategory(id: string | null): void {
    this.categoryId.set(id);
    this.loadArticles();
  }

  selectAttrFilter(value: 'all' | 'top' | 'hot' | 'normal'): void {
    this.attrFilter.set(value);
    this.loadArticles();
  }

  selectTimeFilter(value: 'all' | 'week' | 'month' | 'older'): void {
    this.timeFilter.set(value);
    this.loadArticles();
  }

  /** 发布时间范围（周一起点 / 月初，ISO 字符串） */
  private timeRange(): { after?: string; before?: string } {
    const mode = this.timeFilter();
    if (mode === 'all') return {};
    const now = new Date();
    if (mode === 'older') {
      const monthStart = new Date(now.getFullYear(), now.getMonth(), 1);
      return { before: monthStart.toISOString() };
    }
    const start = mode === 'week'
      ? this.startOfWeek(now)
      : new Date(now.getFullYear(), now.getMonth(), 1);
    return { after: start.toISOString() };
  }

  /** 本周一 00:00（周日归入本周） */
  private startOfWeek(date: Date): Date {
    const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const day = d.getDay();
    d.setDate(d.getDate() - ((day + 6) % 7));
    return d;
  }

  onSearch(): void {
    this.loadArticles();
  }

  /** 在分类树中查找名称（含子级） */
  private findCategoryName(id: string | null): string | null {
    if (!id) return null;
    const walk = (items: NewsCategoryDto[]): string | null => {
      for (const item of items) {
        if (item.id === id) return item.name;
        if (item.children?.length) {
          const found = walk(item.children);
          if (found) return found;
        }
      }
      return null;
    };
    return walk(this.categories());
  }

  /**
   * 文章封面渐变（基于标题 hash，确保稳定）
   */
  coverGradient(article: NewsArticleDto): string {
    return this.gradientByKey(article.title || article.id || 'x', article.categoryName || '');
  }

  private gradientByKey(primary: string, secondary: string): string {
    const palettes = [
      '#2563eb',
      '#1d4ed8',
      '#3b82f6',
      '#0ea5e9',
      '#0284c7',
      '#6366f1',
      '#0891b2',
      '#0c4cb8',
    ];
    const key = (primary || 'x') + (secondary || '');
    let hash = 0;
    for (let i = 0; i < key.length; i++) {
      hash = (hash * 31 + key.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
  }

  hasCover(article: NewsArticleDto): boolean {
    return !!article.coverImageUrl && article.coverImageUrl.trim().length > 0;
  }

  parseTags(tags?: string): string[] {
    if (!tags) return [];
    return tags.split(/[,，;；\s]+/).map(t => t.trim()).filter(t => t.length > 0).slice(0, 3);
  }

  /** 封面图加载失败时隐藏 img，露出底层渐变 pattern */
  onCoverError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (img) {
      img.classList.add('is-hidden');
    }
  }
}
