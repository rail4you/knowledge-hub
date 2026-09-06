import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { MarkdownComponent, MARKED_OPTIONS, provideMarkdown } from 'ngx-markdown';
import { NewsArticleDto, NewsCommentDto, NewsService } from '../../news/news.service';

@Component({
  selector: 'app-student-news-detail',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzButtonModule,
    NzSpinModule,
    NzModalModule,
    MarkdownComponent,
  ],
  providers: [
    // breaks: true 让单个换行也渲染为 <br>，兼容管理端 textarea 的普通换行文本；
    // 同时完整支持 Markdown（标题/加粗/列表/链接/代码等）。
    provideMarkdown({
      markedOptions: {
        provide: MARKED_OPTIONS,
        useValue: { gfm: true, breaks: true },
      },
    }),
  ],
  templateUrl: './student-news-detail.component.html',
  styleUrls: ['./student-news-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentNewsDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly newsService = inject(NewsService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(false);
  readonly article = signal<NewsArticleDto | null>(null);
  readonly comments = signal<NewsCommentDto[]>([]);
  readonly commentText = signal('');

  readonly relatedArticles = signal<NewsArticleDto[]>([]);
  readonly hotArticles = signal<NewsArticleDto[]>([]);
  readonly relatedLoading = signal(false);

  readonly commentLikingIds = signal<Set<string>>(new Set());

  /** 头图加载失败时降级为渐变封面（脏数据/坏链会导致空白占位） */
  readonly coverImgOk = signal(true);
  readonly coverFailedIds = signal<Set<string>>(new Set());

  modalVisible = false;
  submitting = false;

  /** 评论输入框（用于聚焦） */
  readonly commentTextarea = viewChild<ElementRef<HTMLTextAreaElement>>('commentTextarea');

  ngOnInit(): void {
    // 订阅 paramMap：从相关/热门资讯跳转到同一路由的不同 id 时组件会被复用，
    // 只靠 snapshot 的 ngOnInit 不会再次执行，必须监听 paramMap 才能重新加载。
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const id = params.get('id');
      if (!id) {
        this.router.navigate(['/student/news']);
        return;
      }
      this.loadArticle(id);
      this.loadComments(id);
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
        this.router.navigate(['/student/news']);
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
    this.router.navigate(['/student/news']);
  }

  likeComment(comment: NewsCommentDto): void {
    if (this.commentLikingIds().has(comment.id)) return;

    const liked = !comment.userHasLiked;
    // 乐观更新：先翻转态，失败再回滚
    this.applyCommentLike(comment.id, liked);
    this.commentLikingIds.update(set => new Set(set).add(comment.id));
    this.newsService.likeComment(comment.id).subscribe({
      next: updated => {
        this.commentLikingIds.update(set => {
          const next = new Set(set);
          next.delete(comment.id);
          return next;
        });
        this.comments.update(list =>
          list.map(c =>
            c.id === comment.id
              ? { ...c, likeCount: updated.likeCount ?? c.likeCount, userHasLiked: updated.userHasLiked ?? liked }
              : c
          )
        );
      },
      error: err => {
        this.commentLikingIds.update(set => {
          const next = new Set(set);
          next.delete(comment.id);
          return next;
        });
        this.applyCommentLike(comment.id, !liked);
        this.message.error(this.errMsg(err, '点赞失败'));
      },
    });
  }

  private applyCommentLike(id: string, liked: boolean): void {
    this.comments.update(list =>
      list.map(c =>
        c.id === id
          ? { ...c, userHasLiked: liked, likeCount: Math.max(0, (c.likeCount || 0) + (liked ? 1 : -1)) }
          : c
      )
    );
  }

  private errMsg(err: unknown, fallback: string): string {
    const e = err as { error?: { error?: { message?: string }; message?: string }; message?: string };
    return e?.error?.error?.message || e?.error?.message || fallback;
  }

  isCommentLiking(id: string): boolean {
    return this.commentLikingIds().has(id);
  }

  openCommentModal(): void {
    this.modalVisible = true;
    this.commentText.set('');
    // 等待渲染后聚焦输入框
    setTimeout(() => {
      this.commentTextarea()?.nativeElement.focus({ preventScroll: true });
    }, 150);
  }

  closeCommentModal(): void {
    if (this.submitting) return;
    this.modalVisible = false;
    this.commentText.set('');
  }

  submitComment(): void {
    const article = this.article();
    const content = this.commentText().trim();
    if (!article || !content) return;

    this.submitting = true;
    this.newsService.createComment({
      articleId: article.id,
      content,
    }).subscribe({
      next: comment => {
        this.submitting = false;
        this.modalVisible = false;
        this.comments.set([{ ...comment, likeCount: 0, userHasLiked: false }, ...this.comments()]);
        this.commentText.set('');
        this.article.set({
          ...article,
          commentCount: article.commentCount + 1,
        });
        this.message.success('评论已发布');
      },
      error: err => {
        this.submitting = false;
        this.message.error(this.errMsg(err, '评论提交失败'));
      },
    });
  }

  openArticle(id: string): void {
    this.router.navigate(['/student/news', id]);
  }

  /** 资讯封面渐变（与列表页一致） */
  coverGradient(article: NewsArticleDto | { title?: string; id?: string; categoryName?: string }): string {
    return this.gradientByKey(
      article?.title || article?.id || 'x',
      article?.categoryName || ''
    );
  }

  gradientByCategory(name: string): string {
    return this.gradientByKey(name, name);
  }

  private gradientByKey(primary: string, secondary: string): string {
    const palettes = [
      '#1e6ce8',
      '#0891b2',
      '#10b981',
      '#059669',
      '#0284c7',
      '#2563eb',
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

  /** 头图加载失败（脏数据/坏链）→ 降级为渐变封面 */
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

  parseTags(tags?: string): string[] {
    if (!tags) return [];
    return tags
      .split(/[,，;；\s]+/)
      .map(t => t.trim())
      .filter(t => t.length > 0);
  }

  authorInitial(name?: string): string {
    if (!name) return 'S';
    return name.charAt(0).toUpperCase();
  }

  authorGradient(name?: string): string {
    const palettes = [
      '#1e6ce8',
      '#0891b2',
      '#059669',
      '#10b981',
      '#0284c7',
    ];
    const n = name || 'S';
    let hash = 0;
    for (let i = 0; i < n.length; i++) {
      hash = (hash * 31 + n.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
  }

  commentAuthorInitial(name?: string): string {
    if (!name) return 'U';
    return name.charAt(0).toUpperCase();
  }

  commentAuthorGradient(name?: string): string {
    const palettes = [
      '#1e6ce8',
      '#2563eb',
      '#059669',
      '#10b981',
      '#0284c7',
      '#0891b2',
    ];
    const n = name || 'U';
    let hash = 0;
    for (let i = 0; i < n.length; i++) {
      hash = (hash * 31 + n.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
  }

  /** 正文是否有内容（模板空态判断用） */
  hasContent(content?: string): boolean {
    return !!content && content.trim().length > 0;
  }
}
