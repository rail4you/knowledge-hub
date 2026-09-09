import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { MarkdownComponent, MARKED_OPTIONS, provideMarkdown } from 'ngx-markdown';
import { NewsArticleDto, NewsCommentDto, NewsService } from './news.service';
import { fixCjkMarkdown } from '../shared/markdown-cjk-fix.util';

@Component({
  selector: 'app-news-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzInputModule,
    NzSpinModule,
    NzTagModule,
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

  readonly loading = signal(false);
  readonly article = signal<NewsArticleDto | null>(null);
  readonly comments = signal<NewsCommentDto[]>([]);
  readonly commentText = signal('');

  /** CJK 毗邻加粗预处理（这是**文本?**测试 这类写法 marked 原生不渲染）。 */
  readonly fixMarkdown = fixCjkMarkdown;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }

    this.loadArticle(id);
    this.loadComments(id);
  }

  loadArticle(id: string): void {
    this.loading.set(true);
    this.newsService.getArticle(id).subscribe({
      next: article => {
        this.article.set(article);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  loadComments(id: string): void {
    this.newsService.getApprovedComments(id).subscribe({
      next: comments => this.comments.set(comments || []),
    });
  }

  submitComment(): void {
    const article = this.article();
    const content = this.commentText().trim();
    if (!article || !content) {
      return;
    }

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
      error: () => {
        this.message.error('评论提交失败');
      },
    });
  }
}
