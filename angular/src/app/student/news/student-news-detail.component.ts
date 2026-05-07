import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NewsArticleDto, NewsCommentDto, NewsService } from '../../news/news.service';

@Component({
  selector: 'app-student-news-detail',
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
    NzIconModule,
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

  goBack(): void {
    this.router.navigate(['/student/news']);
  }

  likeArticle(): void {
    const article = this.article();
    if (!article || article.userHasLiked) {
      return;
    }

    this.newsService.like(article.id).subscribe({
      next: () => {
        this.article.set({
          ...article,
          userHasLiked: true,
          likeCount: article.likeCount + 1,
        });
        this.message.success('已点赞');
      },
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