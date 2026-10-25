import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MarkdownComponent, MARKED_OPTIONS, provideMarkdown } from 'ngx-markdown';
import { NewsArticleService } from '../../proxy/news/news-article.service';
import { NewsCommentService } from '../../proxy/news/news-comment.service';
import type { NewsArticleDto, NewsCommentDto } from '../../proxy/news/dtos/models';
import { fixCjkMarkdown } from '../../shared/markdown-cjk-fix.util';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';

/** 解析标签字符串为数组（模块级纯函数） */
function splitTagString(tags?: string | null): string[] {
  if (!tags) return [];
  return tags.split(/[,，;；\s]+/).map(t => t.trim()).filter(t => t.length > 0);
}

/**
 * 学生端 · 资讯详情（对齐原型 #article「资讯详情」）
 *
 * 数据来源（全部真实接口，生成代理 proxy/news）：
 *   · 资讯正文 GET  /api/app/news-article/{id}（已下架的资讯学生端不可见）
 *   · 跨库只读预览 GET /api/app/news-article/{id}/published-preview
 *   · 收藏（点赞）POST /api/app/news-article/{id}/like
 *   · 评论列表 GET  /api/app/news-comment/approved-list-by-article/{articleId}
 *   · 发表评论 POST /api/app/news-comment
 *   · 评论点赞 POST /api/app/news-comment/{id}/like
 *   · 相关资讯 GET  /api/app/news-article/published-list?categoryId=...
 *   · 热门资讯 GET  /api/app/news-article/hot-list
 */
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
    MarkdownComponent,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
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
  private readonly router = inject(Router);
  private readonly newsService = inject(NewsArticleService);
  private readonly commentService = inject(NewsCommentService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly article = signal<NewsArticleDto | null>(null);
  readonly comments = signal<NewsCommentDto[]>([]);
  readonly commentsLoading = signal(false);

  /** 评论输入 */
  readonly commentText = signal('');
  readonly replyTo = signal<NewsCommentDto | null>(null);
  readonly submitting = signal(false);
  /** 评论点赞中的评论 id */
  readonly likingIds = signal<Set<string>>(new Set());

  /** 相关资讯 / 热门资讯 */
  readonly relatedArticles = signal<NewsArticleDto[]>([]);
  readonly hotArticles = signal<NewsArticleDto[]>([]);

  /** 收藏（后端为一次性 reaction，不支持取消） */
  readonly collected = signal(false);
  readonly collecting = signal(false);

  /** CJK 毗邻加粗预处理 */
  readonly fixMarkdown = fixCjkMarkdown;

  /** 预览模式：从门户首页跨租户聚合进入，只读（不加载评论、不允许互动） */
  readonly preview = computed(() => this.route.snapshot.queryParamMap.get('from') === 'home');
  readonly sourceTenant = computed(() => this.route.snapshot.queryParamMap.get('tenant'));

  /** 文章标签 */
  readonly articleTags = computed(() => splitTagString(this.article()?.tags));

  /** 一级评论（父评论不在列表中的脏数据兜底为一级展示） */
  readonly rootComments = computed(() => {
    const map = new Map(this.comments().map(c => [c.id, c]));
    return this.comments().filter(c => !c.parentId || !map.has(c.parentId));
  });

  /** 一级评论下的回复（按根评论分组） */
  private readonly repliesByRoot = computed(() => {
    const map = new Map(this.comments().map(c => [c.id, c]));
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
    this.comments().forEach(c => {
      if (!c.parentId) return;
      const rootId = rootOf(c);
      if (!rootId) return;
      const list = grouped.get(rootId);
      if (list) list.push(c);
      else grouped.set(rootId, [c]);
    });
    grouped.forEach(list =>
      list.sort((a, b) => +new Date(a.creationTime || 0) - +new Date(b.creationTime || 0)),
    );
    return grouped;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/student/news']);
      return;
    }
    this.loadArticle(id);
    if (!this.preview()) {
      this.loadComments(id);
    }
    this.loadHot();
  }

  /** 载入正文 */
  private loadArticle(id: string): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    const req = this.preview()
      ? this.newsService.getPublishedPreview(id)
      : this.newsService.get(id);
    req.subscribe({
      next: article => {
        this.article.set(article);
        this.collected.set(!!article?.userHasLiked);
        this.loading.set(false);
        this.loadRelated(article);
      },
      error: () => {
        this.article.set(null);
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  /** 相关资讯（同栏目） */
  private loadRelated(article: NewsArticleDto): void {
    if (!article.categoryId) {
      this.relatedArticles.set([]);
      return;
    }
    this.newsService
      .getPublishedList({ categoryId: article.categoryId, skipCount: 0, maxResultCount: 6 })
      .subscribe({
        next: res =>
          this.relatedArticles.set((res?.items || []).filter(a => a.id !== article.id).slice(0, 5)),
        error: () => this.relatedArticles.set([]),
      });
  }

  /** 热门资讯 */
  private loadHot(): void {
    this.newsService.getHotList(6).subscribe({
      next: list => this.hotArticles.set(list || []),
      error: () => this.hotArticles.set([]),
    });
  }

  /** 载入评论 */
  private loadComments(articleId: string): void {
    this.commentsLoading.set(true);
    this.commentService.getApprovedListByArticle(articleId).subscribe({
      next: list => {
        this.comments.set(list || []);
        this.commentsLoading.set(false);
      },
      error: () => {
        this.comments.set([]);
        this.commentsLoading.set(false);
      },
    });
  }

  /** 收藏（后端 reaction 一次性生效，重复调用不会重复计数） */
  collect(): void {
    const article = this.article();
    if (!article?.id || this.collecting() || this.collected()) return;
    this.collecting.set(true);
    this.newsService.like(article.id).subscribe({
      next: () => {
        this.collecting.set(false);
        this.collected.set(true);
        this.article.update(a => (a ? { ...a, userHasLiked: true, likeCount: (a.likeCount || 0) + 1 } : a));
        this.message.success('已收藏');
      },
      error: err => {
        this.collecting.set(false);
        this.message.error(this.errMsg(err, '收藏失败'));
      },
    });
  }

  /** 发表评论 / 回复 */
  submitComment(): void {
    const article = this.article();
    const content = this.commentText().trim();
    if (!article || !content) return;

    this.submitting.set(true);
    this.commentService
      .create({ articleId: article.id, content, parentId: this.replyTo()?.id ?? null })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.commentText.set('');
          this.replyTo.set(null);
          this.loadComments(article.id);
          this.message.success('评论已提交，审核通过后显示');
        },
        error: err => {
          this.submitting.set(false);
          this.message.error(this.errMsg(err, '评论提交失败'));
        },
      });
  }

  /** 点赞评论 */
  likeComment(comment: NewsCommentDto): void {
    if (!comment.id || this.likingIds().has(comment.id)) return;
    this.likingIds.update(set => new Set(set).add(comment.id));
    this.commentService.like(comment.id).subscribe({
      next: updated => {
        this.likingIds.update(set => {
          const next = new Set(set);
          next.delete(comment.id);
          return next;
        });
        this.comments.update(list =>
          list.map(c =>
            c.id === comment.id
              ? {
                  ...c,
                  likeCount: updated?.likeCount ?? c.likeCount,
                  userHasLiked: updated?.userHasLiked ?? c.userHasLiked,
                }
              : c,
          ),
        );
      },
      error: err => {
        this.likingIds.update(set => {
          const next = new Set(set);
          next.delete(comment.id);
          return next;
        });
        this.message.error(this.errMsg(err, '点赞失败'));
      },
    });
  }

  /** 开始回复 */
  startReply(comment: NewsCommentDto): void {
    this.replyTo.set(comment);
    this.commentText.set('');
  }

  cancelReply(): void {
    this.replyTo.set(null);
  }

  /** 某一级评论下的回复 */
  repliesOf(rootId: string): NewsCommentDto[] {
    return this.repliesByRoot().get(rootId) ?? [];
  }

  /** 回复所@的人名 */
  replyTargetName(reply: NewsCommentDto): string {
    if (!reply.parentId) return '';
    return this.comments().find(c => c.id === reply.parentId)?.userName || '';
  }

  /** 打开其它资讯（保留首页来源参数） */
  openArticle(id: string | undefined): void {
    if (!id) return;
    const qp = this.route.snapshot.queryParamMap;
    const queryParams: Record<string, string> = {};
    const from = qp.get('from');
    const section = qp.get('section');
    if (from) queryParams['from'] = from;
    if (section) queryParams['section'] = section;
    this.router.navigate(['/student/news', id], { queryParams });
  }

  /** 返回 */
  goBack(): void {
    if (this.preview()) {
      this.router.navigate(['/'], { queryParams: { section: 'latest-news' } });
      return;
    }
    this.router.navigate(['/student/news']);
  }

  /** 重新加载 */
  reload(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.loadArticle(id);
  }

  /** 评论作者首字 */
  authorInitial(name?: string | null): string {
    return name ? name.charAt(0).toUpperCase() : 'U';
  }

  private errMsg(err: unknown, fallback: string): string {
    const e = err as { error?: { error?: { message?: string }; message?: string }; message?: string };
    return e?.error?.error?.message || e?.error?.message || fallback;
  }
}
