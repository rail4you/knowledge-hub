import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { MarkdownComponent, MARKED_OPTIONS, provideMarkdown } from 'ngx-markdown';
import { NewsArticleDto, NewsCommentDto, NewsService } from '../../news/news.service';
import { fixCjkMarkdown } from '../../shared/markdown-cjk-fix.util';
import { hashGradient } from '../../shared/utils/color.util';

/** 解析标签字符串为数组（模块级纯函数，便于 computed 复用） */
function splitTagString(tags?: string | null): string[] {
  if (!tags) return [];
  return tags.split(/[,，;；\s]+/).map(t => t.trim()).filter(t => t.length > 0);
}

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

  /** 正在回复的目标评论；null 表示发表一级评论 */
  readonly replyTo = signal<NewsCommentDto | null>(null);

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

  readonly commentLikingIds = signal<Set<string>>(new Set());

  /** 头图加载失败时降级为渐变封面（脏数据/坏链会导致空白占位） */
  readonly coverImgOk = signal(true);
  readonly coverFailedIds = signal<Set<string>>(new Set());

  /** 从门户首页（PortalHome）的最新资讯卡片进入时为 true；返回按钮显示“返回首页”并跳 `/` */
  readonly backToHome = signal<boolean>(false);

  /** 返回按钮文案：从门户首页进入时为“返回首页”，否则为“返回资讯列表” */
  readonly backLabel = computed(() => this.backToHome() ? '返回首页' : '返回资讯列表');

  /** 文章标签：一次性解析，避免模板中重复 split + 分配 */
  readonly articleTags = computed(() => splitTagString(this.article()?.tags));

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
      // 从门户首页（最新资讯卡片）进入时返回按钮回到门户首页
      this.backToHome.set(this.route.snapshot.queryParamMap.get('from') === 'home');
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
    // 从门户首页（最新资讯卡片）进入：直接返回门户首页 `/`
    if (this.backToHome()) {
      this.router.navigate(['/']);
      return;
    }
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
    this.replyTo.set(null);
    this.modalVisible = true;
    this.commentText.set('');
    // 等待渲染后聚焦输入框
    setTimeout(() => {
      this.commentTextarea()?.nativeElement.focus({ preventScroll: true });
    }, 150);
  }

  /** 打开回复弹窗：目标为被回复的一级评论（嵌套回复不再提供回复入口，只支持一级回复） */
  openReplyModal(comment: NewsCommentDto): void {
    this.replyTo.set(comment);
    this.modalVisible = true;
    this.commentText.set('');
    setTimeout(() => {
      this.commentTextarea()?.nativeElement.focus({ preventScroll: true });
    }, 150);
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

  closeCommentModal(): void {
    if (this.submitting) return;
    this.modalVisible = false;
    this.commentText.set('');
    this.replyTo.set(null);
  }

  submitComment(): void {
    const article = this.article();
    const content = this.commentText().trim();
    if (!article || !content) return;

    const target = this.replyTo();
    this.submitting = true;
    this.newsService.createComment({
      articleId: article.id,
      content,
      parentId: target?.id,
    }).subscribe({
      next: () => {
        this.submitting = false;
        this.modalVisible = false;
        this.commentText.set('');
        this.replyTo.set(null);
        // 重新拉取以保证嵌套树与排序一致；条数后端已累加
        this.loadComments(article.id);
        this.article.set({
          ...article,
          commentCount: article.commentCount + 1,
        });
        this.message.success(target ? '回复已发布' : '评论已发布');
      },
      error: err => {
        this.submitting = false;
        this.message.error(this.errMsg(err, target ? '回复提交失败' : '评论提交失败'));
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

  private gradientByKey(primary: string, secondary: string): string {
    const palettes = [
      '#2b6cd4',
      '#0891b2',
      '#10b981',
      '#059669',
      '#2b6cd4',
      '#2b6cd4',
      '#0891b2',
      '#1f56ad',
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

  parseTags(tags?: string): string[] {
    return splitTagString(tags);
  }

  commentAuthorInitial(name?: string): string {
    if (!name) return 'U';
    return name.charAt(0).toUpperCase();
  }

  commentAuthorGradient(name?: string): string {
    const palettes = [
      '#2b6cd4',
      '#2b6cd4',
      '#059669',
      '#10b981',
      '#2b6cd4',
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
