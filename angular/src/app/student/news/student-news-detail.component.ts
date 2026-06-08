import { ChangeDetectionStrategy, Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzRateModule } from 'ng-zorro-antd/rate';
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
    NzRateModule,
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

  readonly loading = signal(false);
  readonly article = signal<NewsArticleDto | null>(null);
  readonly comments = signal<NewsCommentDto[]>([]);
  readonly commentText = signal('');

  readonly relatedArticles = signal<NewsArticleDto[]>([]);
  readonly hotArticles = signal<NewsArticleDto[]>([]);
  readonly relatedLoading = signal(false);

  readonly copyLinkSuccess = signal(false);

  /** 评论区根元素（用于滚动定位） */
  readonly commentSection = viewChild<ElementRef<HTMLElement>>('commentSection');
  /** 评论输入框（用于聚焦） */
  readonly commentTextarea = viewChild<ElementRef<HTMLTextAreaElement>>('commentTextarea');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/student/news']);
      return;
    }

    this.loadArticle(id);
    this.loadComments(id);
    this.loadHot();
  }

  loadArticle(id: string): void {
    this.loading.set(true);
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

  likeArticle(): void {
    const article = this.article();
    if (!article || article.userHasLiked) return;

    this.newsService.like(article.id).subscribe({
      next: () => {
        this.article.set({
          ...article,
          userHasLiked: true,
          likeCount: article.likeCount + 1,
        });
        this.message.success('已点赞');
      },
      error: () => this.message.error('点赞失败'),
    });
  }

  copyLink(): void {
    const article = this.article();
    if (!article?.id) return;
    const url = `${window.location.origin}/student/news/${article.id}`;

    const showSuccess = () => {
      this.copyLinkSuccess.set(true);
      this.message.success('链接已复制到剪贴板');
      setTimeout(() => this.copyLinkSuccess.set(false), 1800);
    };

    const fallback = () => {
      const input = document.createElement('input');
      input.value = url;
      document.body.appendChild(input);
      input.select();
      try {
        document.execCommand('copy');
        showSuccess();
      } catch {
        this.message.error('复制失败，请手动复制');
      }
      document.body.removeChild(input);
    };

    if (navigator.clipboard) {
      navigator.clipboard.writeText(url).then(showSuccess).catch(fallback);
    } else {
      fallback();
    }
  }

  submitComment(): void {
    const article = this.article();
    const content = this.commentText().trim();
    if (!article || !content) return;

    this.newsService.createComment({
      articleId: article.id,
      content,
    }).subscribe({
      next: comment => {
        this.comments.set([comment, ...this.comments()]);
        this.commentText.set('');
        this.article.set({
          ...article,
          commentCount: article.commentCount + 1,
        });
        this.message.success('评论已发布');
      },
      error: () => this.message.error('评论提交失败'),
    });
  }

  openArticle(id: string): void {
    this.router.navigate(['/student/news', id]);
  }

  /**
   * 滚动到评论区，并聚焦评论输入框（如果已开启评论）。
   * 修复 bug：旧实现是 `<a href="#comments">`，在某些路由配置下被 Angular 路由器误解为
   * 路由片段，回退到首页。改为按钮事件后由组件显式处理。
   */
  focusComment(): void {
    const section = this.commentSection()?.nativeElement;
    section?.scrollIntoView({ behavior: 'smooth', block: 'start' });

    // 评论可能被作者关闭，此时 textarea 不存在
    const textarea = this.commentTextarea()?.nativeElement;
    if (textarea) {
      // 等待滚动开始 + 内容渲染后再聚焦，避免 iOS Safari 因聚焦中断滚动
      setTimeout(() => textarea.focus({ preventScroll: true }), 350);
    }
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

  /** 渲染文章正文（处理段落、空行） */
  getContentParagraphs(content: string): string[] {
    if (!content) return [];
    return content
      .split(/\n\s*\n/)
      .map(p => p.trim())
      .filter(p => p.length > 0);
  }
}
