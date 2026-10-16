import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NewsArticleDto, NewsCategoryDto, NewsService } from './news.service';
import { ClientCacheService } from '../shared/cache/client-cache.service';
import { HeadlineHeroComponent, HeadlineHeroStat } from '../student/shared/headline-hero/headline-hero.component';

/** 资讯列表页缓存命名空间（通用 ClientCacheService，TTL 60s） */
const NEWS_CACHE_NS = 'news';

@Component({
  selector: 'app-news-list',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    NzIconModule,
    NzSpinModule,
    NzPaginationModule,
    HeadlineHeroComponent,
  ],
  templateUrl: './news-list.component.html',
  styleUrls: ['./news-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsListComponent implements OnInit {
  private readonly newsService = inject(NewsService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly cache = inject(ClientCacheService);

  readonly loading = signal(false);
  readonly articles = signal<NewsArticleDto[]>([]);
  readonly totalCount = signal(0);
  /** 分页：默认每页 10 条（服务端分页） */
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly hotArticles = signal<NewsArticleDto[]>([]);
  /** 头条文章列表（第 0 个作为主推） */
  readonly headlineArticles = signal<NewsArticleDto[]>([]);
  readonly categories = signal<NewsCategoryDto[]>([]);
  readonly filter = signal('');
  readonly categoryId = signal<string | null>(null);
  /** 文章属性筛选：all 全部，top 头条，hot 热门，normal 一般文章 */
  readonly attrFilter = signal<'all' | 'top' | 'hot' | 'normal'>('all');
  /** 发布时间筛选：all 全部，week 本周，month 本月，older 更早 */
  readonly timeFilter = signal<'all' | 'week' | 'month' | 'older'>('all');

  // 数据统计（从实际数据计算）
  readonly stats = computed<HeadlineHeroStat[]>(() => {
    const all = this.articles();
    const hot = this.hotArticles();
    const totalViews = all.reduce((sum, a) => sum + (a.viewCount || 0), 0);
    const totalLikes = all.reduce((sum, a) => sum + (a.likeCount || 0), 0);
    return [
      { label: '资讯总数', value: this.totalCount(), suffix: '篇', icon: 'file-text', color: '#087f7a' },
      { label: '热门资讯', value: hot.length, suffix: '篇', icon: 'fire', color: '#f59e0b' },
      { label: '总阅读量', value: totalViews, suffix: totalViews >= 10000 ? '万次' : '次', icon: 'eye', color: '#10b981' },
      { label: '总点赞量', value: totalLikes, suffix: totalLikes >= 10000 ? '万次' : '次', icon: 'like', color: '#06b6d4' },
    ];
  });

  ngOnInit(): void {
    this.loadCategories();
    this.loadArticles();
    this.loadHotArticles();
    this.loadHeadlineArticles();
  }

  loadCategories(): void {
    this.cache.load<NewsCategoryDto[]>(NEWS_CACHE_NS, 'categories', () => this.newsService.getCategoryTree()).subscribe({
      next: categories => {
        this.categories.set(categories || []);
      },
    });
  }

  loadArticles(): void {
    const attr = this.attrFilter();
    const time = this.timeRange();
    const input = {
      filter: this.filter() || undefined,
      categoryId: this.categoryId() || undefined,
      isTop: attr === 'top' ? true : attr === 'normal' ? false : undefined,
      isHot: attr === 'hot' ? true : attr === 'normal' ? false : undefined,
      publishedAfter: time.after,
      publishedBefore: time.before,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    };

    this.loading.set(true);
    this.cache.load<any>(NEWS_CACHE_NS, `list:${JSON.stringify(input)}`, () => this.newsService.getPublishedArticles(input)).subscribe({
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
    this.cache.load<NewsArticleDto[]>(NEWS_CACHE_NS, 'hot', () => this.newsService.getHotArticles()).subscribe({
      next: items => this.hotArticles.set(items || []),
    });
  }

  /** 拉取头条文章（isTop=true，按发布时间倒序，取前 4 篇） */
  loadHeadlineArticles(): void {
    this.cache.load<any>(NEWS_CACHE_NS, 'headlines', () => this.newsService.getPublishedArticles({
      isTop: true,
      skipCount: 0,
      maxResultCount: 4,
    })).subscribe({
      next: result => this.headlineArticles.set(result.items || []),
      error: () => this.headlineArticles.set([]),
    });
  }

  openArticle(id: string): void {
    this.router.navigate(['/news', id]);
  }

  selectCategory(id: string | null): void {
    this.categoryId.set(id);
    this.pageIndex.set(1);
    this.loadArticles();
  }

  selectAttrFilter(value: 'all' | 'top' | 'hot' | 'normal'): void {
    this.attrFilter.set(value);
    this.pageIndex.set(1);
    this.loadArticles();
  }

  selectTimeFilter(value: 'all' | 'week' | 'month' | 'older'): void {
    this.timeFilter.set(value);
    this.pageIndex.set(1);
    this.loadArticles();
  }

  /** 翻页：服务端分页，回到列表顶部 */
  onPageChange(index: number): void {
    this.pageIndex.set(index);
    this.loadArticles();
    const el = document.querySelector('.news-card');
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
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
    this.pageIndex.set(1);
    this.loadArticles();
  }
}
