import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MarkdownComponent, MARKED_OPTIONS, provideMarkdown } from 'ngx-markdown';
import { NewsArticleDto, NewsCommentDto, NewsService } from './news.service';
import { fixCjkMarkdown } from '../shared/markdown-cjk-fix.util';
import { hashGradient } from '../shared/utils/color.util';

/** 解析标签字符串为数组（模块级纯函数，便于 computed 复用） */
function splitTagString(tags?: string | null): string[] {
  if (!tags) return [];
  return tags.split(/[,，;；\s]+/).map(t => t.trim()).filter(t => t.length > 0);
}

@Component({
  selector: 'app-news-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    MarkdownComponent,
  ],
  providers: [
    // 与学生端一致：单个换行渲染为 <br>，同时支持 Markdown。
    provideMarkdown({
      markedOptions: {
        provide: MARKED_OPTIONS,
        useValue: { gfm: true, breaks: true },
      },
    }),
  ],
  templateUrl: './news-detail.component.html',
  styleUrls: ['./news-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly newsService = inject(NewsService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(false);
  readonly article = signal<NewsArticleDto | null>(null);
  /** 评论区为只读：仅加载展示，不提供发表/回复/点赞入口 */
  readonly comments = signal<NewsCommentDto[]>([]);

  /** CJK 毗邻加粗预处理（这是**文本?**测试 这类写法 marked 原生不渲染）。 */
  readonly fixMarkdown = fixCjkMarkdown;

  /** 评论 id → 评论（回复链查找与孤儿兜底用） */
  readonly commentMap = computed(() => new Map(this.comments().map(c => [c.id, c])));

  /** 一级评论：无 parentId，或父评论不在列表中（脏数据兜底为一级展示） */
  readonly rootComments = computed(() => {
    const map = this.commentMap();
    return this.comments().filter(c => !c.parentId || !map.has(c.parentId));
  });

  readonly relatedArticles = signal<NewsArticleDto[]>([]);
  readonly hotArticles = signal<NewsArticleDto[]>([]);
  readonly relatedLoading = signal(false);

  /** 头图加载失败时降级为渐变封面（脏数据/坏链会导致空白占位） */
  readonly coverImgOk = signal(true);
  readonly coverFailedIds = signal<Set<string>>(new Set());

  /** 文章标签：一次性解析，避免模板中重复 split + 分配 */
  readonly articleTags = computed(() => splitTagString(this.article()?.tags));

  ngOnInit(): void {
    // 订阅 paramMap：从相关/热门资讯跳转到同一路由的不同 id 时组件会被复用，
    // 只靠 snapshot 的 ngOnInit 不会再次执行，必须监听 paramMap 才能重新加载。
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const id = params.get('id');
      if (!id) {
        this.router.navigate(['/news']);
        return;
      }
      this.loadComments(id);
      this.loadArticle(id);
      this.loadHot();
    });
  }

  loadArticle(id: string): void {
    this.loading.set(true);
    this.coverImgOk.set(true);
    this.newsService.getArticle(id).subscribe({
      next: article => {
        this.article.set(article);
        this.loading.set(false);
        this.loadRelated(article);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('资讯加载失败');
        this.router.navigate(['/news']);
      },
    });
  }

  loadComments(id: string): void {
    this.newsService.getApprovedComments(id).subscribe({
      next: comments => this.comments.set(comments || []),
    });
  }

  loadRelated(article: NewsArticleDto): void {
    this.relatedLoading.set(true);
    // 优先加载同分类的资讯
    if (article.categoryId) {
      this.newsService.getPublishedArticles({
        categoryId: article.categoryId,
        skipCount: 0,
        maxResultCount: 6,
      }).subscribe({
        next: result => {
          const items = (result.items || []).filter(a => a.id !== article.id).slice(0, 5);
          this.relatedArticles.set(items);
          this.relatedLoading.set(false);
        },
        error: () => {
          this.relatedLoading.set(false);
          this.loadHot();
        },
      });
    } else {
      this.relatedLoading.set(false);
      this.loadHot();
    }
  }

  loadHot(): void {
    this.newsService.getHotArticles(6).subscribe({
      next: items => this.hotArticles.set(items || []),
    });
  }

  goBack(): void {
    this.router.navigate(['/news']);
  }

  openArticle(id: string): void {
    this.router.navigate(['/news', id]);
  }

  /** 某一级评论下的全部回复（含楼中楼，统一挂根下按时间正序展示） */
  repliesOf(rootId: string): NewsCommentDto[] {
    return this.repliesByRoot().get(rootId) ?? [];
  }

  /** 评论变化时一次性归组，避免模板对每个根评论重复扫描整棵评论树（O(n²)） */
  private readonly repliesByRoot = computed(() => {
    const map = this.commentMap();
    const grouped = new Map<string, NewsCommentDto[]>();
    const rootOf = (c: NewsCommentDto): string | null => {
      let pid = c.parentId;
      const seen = new Set<string>([c.id]);
      while (pid) {
        if (seen.has(pid)) return null;
        seen.add(pid);
        const parent = map.get(pid);
        if (!parent) return null;
        if (!parent.parentId) return parent.id;
        pid = parent.parentId;
      }
      return null;
    };
    for (const c of this.comments()) {
      if (!c.parentId) continue;
      const rootId = rootOf(c);
      if (!rootId) continue;
      const list = grouped.get(rootId);
      if (list) list.push(c);
      else grouped.set(rootId, [c]);
    }
    for (const list of grouped.values()) {
      list.sort((a, b) => +new Date(a.creationTime) - +new Date(b.creationTime));
    }
    return grouped;
  });

  /** 回复直接 @ 的人名（父评论作者；父为根时模板不展示） */
  replyTargetName(reply: NewsCommentDto): string {
    if (!reply.parentId) return '';
    return this.commentMap().get(reply.parentId)?.userName || '';
  }

  /** 资讯封面渐变（与列表页一致） */
  coverGradient(article: NewsArticleDto | { title?: string; id?: string; categoryName?: string }): string {
    return this.gradientByKey(
      article?.title || article?.id || 'x',
      article?.categoryName || ''
    );
  }

  private gradientByKey(primary: string, secondary: string): string {
    const palettes = [
      '#087f7a',
      '#0891b2',
      '#10b981',
      '#059669',
      '#087f7a',
      '#087f7a',
      '#0891b2',
      '#086964',
    ];
    const key = (primary || 'x') + (secondary || '');
    return hashGradient(key, palettes);
  }

  hasCover(article: NewsArticleDto): boolean {
    return !!article.coverImageUrl && article.coverImageUrl.trim().length > 0;
  }

  /** 头图加载失败（脏数据/坏链）→ 降级显示占位图 */
  onCoverError(): void {
    this.coverImgOk.set(false);
  }

  showCover(article: NewsArticleDto): boolean {
    return this.hasCover(article) && this.coverImgOk();
  }

  onRelatedCoverError(id: string): void {
    this.coverFailedIds.update(set => new Set(set).add(id));
  }

  showRelatedCover(r: NewsArticleDto): boolean {
    return this.hasCover(r) && !this.coverFailedIds().has(r.id);
  }

  commentAuthorInitial(name?: string): string {
    if (!name) return 'U';
    return name.charAt(0).toUpperCase();
  }

  commentAuthorGradient(name?: string): string {
    const palettes = [
      '#087f7a',
      '#087f7a',
      '#059669',
      '#10b981',
      '#087f7a',
      '#0891b2',
    ];
    const n = name || 'U';
    return hashGradient(n, palettes);
  }

  /** 正文是否有内容（模板空态判断用） */
  hasContent(content?: string): boolean {
    return !!content && content.trim().length > 0;
  }
}
