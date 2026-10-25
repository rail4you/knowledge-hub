import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DestroyRef } from '@angular/core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NewsArticleService } from '../../proxy/news/news-article.service';
import { NewsCategoryService } from '../../proxy/news/news-category.service';
import type { NewsArticleDto, NewsCategoryDto } from '../../proxy/news/dtos/models';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';

/**
 * 学生端 · 资讯中心（对齐原型 #news「资讯与发展」）
 *
 * 数据来源（全部真实接口，生成代理 proxy/news）：
 *   · 资讯列表 GET  /api/app/news-article/published-list（服务端分页：skipCount/maxResultCount）
 *   · 热门资讯 GET  /api/app/news-article/hot-list
 *   · 资讯栏目 GET  /api/app/news-category/active-list
 *
 * 筛选条件（栏目 / 关键词 / 页码）全部写进 URL query params，刷新与分享不丢状态。
 * 四个状态齐备：loading(skeleton) / error(retry) / empty / data。
 */
@Component({
  selector: 'app-student-news',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
  ],
  templateUrl: './student-news.component.html',
  styleUrls: ['./student-news.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentNewsComponent implements OnInit {
  private readonly newsService = inject(NewsArticleService);
  private readonly categoryService = inject(NewsCategoryService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);

  readonly articles = signal<NewsArticleDto[]>([]);
  readonly totalCount = signal(0);
  /** 全部已发布资讯条数（用于页头数据总览，不受筛选影响） */
  readonly publishedTotal = signal(0);
  readonly categories = signal<NewsCategoryDto[]>([]);
  /** 头条：置顶资讯的第一条（真实接口 published-list?isTop=true） */
  readonly headline = signal<NewsArticleDto | null>(null);
  /** 热门资讯（真实接口 hot-list） */
  readonly hotArticles = signal<NewsArticleDto[]>([]);

  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  /** 与 URL 同步的筛选状态 */
  readonly categoryId = signal<string | null>(null);
  readonly keyword = signal('');

  /** 搜索框输入（未提交） */
  readonly keywordInput = signal('');

  /** 当前栏目名称（页签文案 + 结果标题） */
  readonly currentCategoryName = computed(() => {
    const id = this.categoryId();
    if (!id) return '全部资讯';
    return this.categories().find(c => c.id === id)?.name || '全部资讯';
  });

  /** 页头数据总览（每个数字都来自真实接口） */
  readonly heroStats = computed(() => [
    { label: '已发布资讯', value: this.publishedTotal(), suffix: '篇', icon: 'file-text' },
    { label: '资讯栏目', value: this.categories().length, suffix: '个', icon: 'appstore' },
    { label: '热门推荐', value: this.hotArticles().length, suffix: '篇', icon: 'fire' },
  ]);

  /** 是否处于「筛选后无结果」状态（用于区分 data / result 空态） */
  readonly hasFilter = computed(() => !!this.categoryId() || !!this.keyword().trim());

  ngOnInit(): void {
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const cat = params.get('cat');
      const q = params.get('q') || '';
      const page = Number(params.get('page') || '1');
      this.categoryId.set(cat && cat !== 'all' ? cat : null);
      this.keyword.set(q);
      this.keywordInput.set(q);
      this.pageIndex.set(Number.isFinite(page) && page > 0 ? page : 1);
      this.loadList();
    });

    this.loadCategories();
    this.loadHeadline();
    this.loadHot();
    this.loadPublishedTotal();
  }

  /** 栏目（分类）——只取启用中的栏目 */
  private loadCategories(): void {
    this.categoryService.getActiveList().subscribe({
      next: list => this.categories.set(list || []),
      error: () => this.categories.set([]),
    });
  }

  /** 头条：置顶资讯 */
  private loadHeadline(): void {
    this.newsService
      .getPublishedList({ isTop: true, skipCount: 0, maxResultCount: 1 })
      .subscribe({
        next: res => this.headline.set(res?.items?.[0] ?? null),
        error: () => this.headline.set(null),
      });
  }

  /** 热门资讯 */
  private loadHot(): void {
    this.newsService.getHotList(5).subscribe({
      next: list => this.hotArticles.set(list || []),
      error: () => this.hotArticles.set([]),
    });
  }

  /** 全部已发布资讯条数（只取 totalCount，不取列表） */
  private loadPublishedTotal(): void {
    this.newsService.getPublishedList({ skipCount: 0, maxResultCount: 1 }).subscribe({
      next: res => this.publishedTotal.set(res?.totalCount ?? 0),
      error: () => this.publishedTotal.set(0),
    });
  }

  /** 资讯列表（服务端分页） */
  private loadList(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.newsService
      .getPublishedList({
        filter: this.keyword().trim() || undefined,
        categoryId: this.categoryId() || undefined,
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: res => {
          this.articles.set(res?.items || []);
          this.totalCount.set(res?.totalCount ?? 0);
          this.loading.set(false);
        },
        error: () => {
          this.articles.set([]);
          this.totalCount.set(0);
          this.loadFailed.set(true);
          this.loading.set(false);
        },
      });
  }

  /** 重新加载 */
  reload(): void {
    this.loadList();
  }

  /** 切换栏目（写回 URL） */
  selectCategory(id: string | null): void {
    this.writeUrl({ cat: id, page: null });
  }

  /** 提交搜索（写回 URL） */
  applySearch(): void {
    const q = this.keywordInput().trim();
    this.writeUrl({ q: q || null, page: null });
  }

  /** 清除筛选 */
  clearFilters(): void {
    this.keywordInput.set('');
    this.writeUrl({ cat: null, q: null, page: null });
  }

  /** 翻页（写回 URL） */
  onPageChange(e: { pageIndex: number; pageSize: number }): void {
    this.pageSize.set(e.pageSize);
    this.writeUrl({ page: e.pageIndex > 1 ? String(e.pageIndex) : null });
  }

  /** 打开文章详情 */
  openArticle(id: string | undefined): void {
    if (!id) return;
    this.router.navigate(['/student/news', id]);
  }

  /**
   * 统一写回 URL：未使用的参数一律删除，保证地址栏干净。
   * patch 语义：undefined = 保持原值，null = 删除该参数，字符串 = 设为该值。
   */
  private writeUrl(patch: { cat?: string | null; q?: string | null; page?: string | null }): void {
    const current = this.route.snapshot.queryParamMap;
    const next: Record<string, string> = {
      cat: patch.cat !== undefined ? patch.cat ?? '' : current.get('cat') ?? '',
      q: patch.q !== undefined ? patch.q ?? '' : current.get('q') ?? '',
      page: patch.page !== undefined ? patch.page ?? '' : current.get('page') ?? '',
    };
    const queryParams: Record<string, string> = {};
    Object.entries(next).forEach(([k, v]) => {
      if (v) queryParams[k] = v;
    });
    this.router.navigate([], { relativeTo: this.route, queryParams, replaceUrl: true });
  }
}
