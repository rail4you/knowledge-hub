import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import {
  CreateUpdateNewsArticleDto,
  CreateUpdateNewsCategoryDto,
  NewsArticleDto,
  NewsArticleStatus,
  NewsCategoryDto,
  NewsCommentDto,
  NewsCommentStatus,
  NewsService,
} from '../../news/news.service';

@Component({
  selector: 'app-news-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSwitchModule,
    NzTableModule,
    NzTagModule,
    NzTabsModule,
  ],
  templateUrl: './news-management.component.html',
  styleUrls: ['./news-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsManagementComponent implements OnInit {
  private readonly newsService = inject(NewsService);
  private readonly message = inject(NzMessageService);

  readonly categories = signal<NewsCategoryDto[]>([]);
  readonly articles = signal<NewsArticleDto[]>([]);
  readonly comments = signal<NewsCommentDto[]>([]);

  categoryModalVisible = false;
  editingCategoryId: string | null = null;
  categoryForm: CreateUpdateNewsCategoryDto = this.createEmptyCategory();

  articleModalVisible = false;
  editingArticleId: string | null = null;
  articleForm: CreateUpdateNewsArticleDto = this.createEmptyArticle();

  activeTabIndex = 0;
  readonly articleStatuses = NewsArticleStatus;
  readonly commentStatuses = NewsCommentStatus;

  ngOnInit(): void {
    this.reloadAll();
  }

  reloadAll(): void {
    this.loadCategories();
    this.loadArticles();
    this.loadComments();
  }

  createEmptyCategory(): CreateUpdateNewsCategoryDto {
    return {
      name: '',
      code: '',
      sortOrder: 0,
      isActive: true,
    };
  }

  createEmptyArticle(): CreateUpdateNewsArticleDto {
    return {
      categoryId: '',
      title: '',
      summary: '',
      content: '',
      coverImageUrl: '',
      tags: '',
      isTop: false,
      isHot: false,
      allowComments: true,
    };
  }

  loadCategories(): void {
    this.newsService.getCategoryList().subscribe({
      next: result => this.categories.set(result.items || []),
    });
  }

  loadArticles(): void {
    this.newsService.getArticleList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe({
      next: result => this.articles.set(result.items || []),
    });
  }

  loadComments(): void {
    this.newsService.getCommentList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe({
      next: result => this.comments.set(result.items || []),
    });
  }

  openCreateCategory(): void {
    this.editingCategoryId = null;
    this.categoryForm = this.createEmptyCategory();
    this.categoryModalVisible = true;
  }

  openEditCategory(item: NewsCategoryDto): void {
    this.editingCategoryId = item.id;
    this.categoryForm = {
      parentId: item.parentId,
      name: item.name,
      code: item.code,
      sortOrder: item.sortOrder,
      isActive: item.isActive,
    };
    this.categoryModalVisible = true;
  }

  saveCategory(): void {
    // 自动根据名称生成编码，用户无需手动填写
    if (!this.categoryForm.code || this.categoryForm.code.trim() === '') {
      this.categoryForm.code = this.categoryForm.name
        ? this.categoryForm.name.trim().toLowerCase().replace(/\s+/g, '-') + '-' + Date.now().toString(36)
        : 'cat-' + Date.now().toString(36);
    }

    const request = this.editingCategoryId
      ? this.newsService.updateCategory(this.editingCategoryId, this.categoryForm)
      : this.newsService.createCategory(this.categoryForm);

    request.subscribe({
      next: () => {
        this.categoryModalVisible = false;
        this.message.success('分类已保存');
        this.loadCategories();
      },
      error: (err) => {
        const msg = err?.error?.error?.message || err?.error?.message || '分类保存失败';
        this.message.error(msg);
      },
    });
  }

  deleteCategory(id: string): void {
    this.newsService.deleteCategory(id).subscribe({
      next: () => {
        this.message.success('分类已删除');
        this.loadCategories();
      },
      error: () => this.message.error('分类删除失败'),
    });
  }

  openCreateArticle(): void {
    this.editingArticleId = null;
    this.articleForm = this.createEmptyArticle();
    this.articleModalVisible = true;
  }

  openEditArticle(item: NewsArticleDto): void {
    this.editingArticleId = item.id;
    this.articleForm = {
      categoryId: item.categoryId,
      title: item.title,
      summary: item.summary || '',
      content: item.content,
      coverImageUrl: item.coverImageUrl || '',
      tags: item.tags || '',
      isTop: item.isTop,
      isHot: item.isHot,
      allowComments: item.allowComments,
    };
    this.articleModalVisible = true;
  }

  saveArticle(): void {
    const request = this.editingArticleId
      ? this.newsService.updateArticle(this.editingArticleId, this.articleForm)
      : this.newsService.createArticle(this.articleForm);

    request.subscribe({
      next: () => {
        this.articleModalVisible = false;
        this.message.success('资讯已保存');
        this.loadArticles();
      },
      error: (err) => {
        const msg = err?.error?.error?.message || err?.error?.message || '资讯保存失败';
        this.message.error(msg);
      },
    });
  }

  submitForReview(id: string): void {
    this.newsService.submitForReview(id).subscribe({
      next: () => {
        this.message.success('已提交审核');
        this.loadArticles();
      },
    });
  }

  publish(id: string): void {
    this.newsService.publish(id).subscribe({
      next: () => {
        this.message.success('已发布');
        this.loadArticles();
      },
    });
  }

  reject(id: string): void {
    this.newsService.reject(id, { comment: '后台驳回' }).subscribe({
      next: () => {
        this.message.success('已驳回');
        this.loadArticles();
      },
    });
  }

  archive(id: string): void {
    this.newsService.archive(id, { comment: '后台下线' }).subscribe({
      next: () => {
        this.message.success('已归档');
        this.loadArticles();
      },
    });
  }

  deleteArticle(id: string): void {
    this.newsService.deleteArticle(id).subscribe({
      next: () => {
        this.message.success('资讯已删除');
        this.loadArticles();
      },
      error: () => this.message.error('资讯删除失败'),
    });
  }

  reviewComment(id: string, status: NewsCommentStatus): void {
    this.newsService.reviewComment(id, status).subscribe({
      next: () => {
        this.message.success('评论状态已更新');
        this.loadComments();
        this.loadArticles();
      },
    });
  }

  deleteComment(id: string): void {
    this.newsService.deleteComment(id).subscribe({
      next: () => {
        this.message.success('评论已删除');
        this.loadComments();
        this.loadArticles();
      },
    });
  }

  getArticleStatusLabel(status: NewsArticleStatus): string {
    const labels: Record<number, string> = {
      [NewsArticleStatus.Draft]: '草稿',
      [NewsArticleStatus.PendingReview]: '待审核',
      [NewsArticleStatus.Published]: '已发布',
      [NewsArticleStatus.Rejected]: '已驳回',
      [NewsArticleStatus.Archived]: '已归档',
    };
    return labels[status] || '未知';
  }

  getCommentStatusLabel(status: NewsCommentStatus): string {
    const labels: Record<number, string> = {
      [NewsCommentStatus.Approved]: '显示中',
      [NewsCommentStatus.Hidden]: '已隐藏',
      [NewsCommentStatus.Rejected]: '已拒绝',
    };
    return labels[status] || '未知';
  }
}
