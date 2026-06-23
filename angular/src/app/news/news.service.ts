import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Observable } from 'rxjs';

export enum NewsArticleStatus {
  Draft = 0,
  PendingReview = 1,
  Published = 2,
  Rejected = 3,
  Archived = 4,
}

export enum NewsCommentStatus {
  Approved = 0,
  Hidden = 1,
  Rejected = 2,
}

export interface NewsCategoryDto {
  id: string;
  parentId?: string;
  name: string;
  code: string;
  sortOrder: number;
  isActive: boolean;
  children?: NewsCategoryDto[];
}

export interface CreateUpdateNewsCategoryDto {
  parentId?: string;
  name: string;
  code: string;
  sortOrder: number;
  isActive: boolean;
}

export interface NewsArticleDto {
  id: string;
  categoryId: string;
  categoryName?: string;
  title: string;
  summary?: string;
  content: string;
  coverImageUrl?: string;
  tags?: string;
  isTop: boolean;
  isHot: boolean;
  allowComments: boolean;
  status: NewsArticleStatus;
  authorId?: string;
  authorName?: string;
  publishedAt?: string;
  viewCount: number;
  likeCount: number;
  commentCount: number;
  userHasLiked: boolean;
  creationTime: string;
}

export interface CreateUpdateNewsArticleDto {
  categoryId: string;
  title: string;
  summary?: string;
  content: string;
  coverImageUrl?: string;
  tags?: string;
  isTop: boolean;
  isHot: boolean;
  allowComments: boolean;
}

export interface PagedNewsArticleRequestDto {
  filter?: string;
  categoryId?: string;
  status?: NewsArticleStatus;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface ReviewNewsArticleDto {
  comment?: string;
}

export interface NewsCommentDto {
  id: string;
  articleId: string;
  parentId?: string;
  userId: string;
  userName?: string;
  content: string;
  status: NewsCommentStatus;
  creationTime: string;
}

export interface CreateNewsCommentDto {
  articleId: string;
  parentId?: string;
  content: string;
}

export interface PagedNewsCommentRequestDto {
  articleId?: string;
  status?: NewsCommentStatus;
  filter?: string;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

@Injectable({
  providedIn: 'root',
})
export class NewsService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getCategoryList(): Observable<PagedResultDto<NewsCategoryDto>> {
    return this.restService.request<any, PagedResultDto<NewsCategoryDto>>({
      method: 'GET',
      url: '/api/app/news-category',
      params: { skipCount: 0, maxResultCount: 100 },
    }, { apiName: this.apiName });
  }

  getCategoryTree(): Observable<NewsCategoryDto[]> {
    return this.restService.request<any, NewsCategoryDto[]>({
      method: 'GET',
      url: '/api/app/news-category/tree',
    }, { apiName: this.apiName });
  }

  createCategory(input: CreateUpdateNewsCategoryDto): Observable<NewsCategoryDto> {
    return this.restService.request<any, NewsCategoryDto>({
      method: 'POST',
      url: '/api/app/news-category',
      body: input,
    }, { apiName: this.apiName });
  }

  updateCategory(id: string, input: CreateUpdateNewsCategoryDto): Observable<NewsCategoryDto> {
    return this.restService.request<any, NewsCategoryDto>({
      method: 'PUT',
      url: `/api/app/news-category/${id}`,
      body: input,
    }, { apiName: this.apiName });
  }

  deleteCategory(id: string): Observable<void> {
    return this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/news-category/${id}`,
    }, { apiName: this.apiName });
  }

  getPublishedArticles(input: PagedNewsArticleRequestDto): Observable<PagedResultDto<NewsArticleDto>> {
    return this.restService.request<any, PagedResultDto<NewsArticleDto>>({
      method: 'GET',
      url: '/api/app/news-article/published-list',
      params: input,
    }, { apiName: this.apiName });
  }

  getArticleList(input: PagedNewsArticleRequestDto): Observable<PagedResultDto<NewsArticleDto>> {
    return this.restService.request<any, PagedResultDto<NewsArticleDto>>({
      method: 'GET',
      url: '/api/app/news-article',
      params: input,
    }, { apiName: this.apiName });
  }

  getArticle(id: string): Observable<NewsArticleDto> {
    return this.restService.request<any, NewsArticleDto>({
      method: 'GET',
      url: `/api/app/news-article/${id}`,
    }, { apiName: this.apiName });
  }

  getHotArticles(maxCount = 8): Observable<NewsArticleDto[]> {
    return this.restService.request<any, NewsArticleDto[]>({
      method: 'GET',
      url: '/api/app/news-article/hot-list',
      params: { maxCount },
    }, { apiName: this.apiName });
  }

  createArticle(input: CreateUpdateNewsArticleDto): Observable<NewsArticleDto> {
    return this.restService.request<any, NewsArticleDto>({
      method: 'POST',
      url: '/api/app/news-article',
      body: input,
    }, { apiName: this.apiName });
  }

  updateArticle(id: string, input: CreateUpdateNewsArticleDto): Observable<NewsArticleDto> {
    return this.restService.request<any, NewsArticleDto>({
      method: 'PUT',
      url: `/api/app/news-article/${id}`,
      body: input,
    }, { apiName: this.apiName });
  }

  deleteArticle(id: string): Observable<void> {
    return this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/news-article/${id}`,
    }, { apiName: this.apiName });
  }

  submitForReview(id: string): Observable<void> {
    return this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/news-article/${id}/submit-for-review`,
    }, { apiName: this.apiName });
  }

  publish(id: string): Observable<void> {
    return this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/news-article/${id}/publish`,
    }, { apiName: this.apiName });
  }

  reject(id: string, input: ReviewNewsArticleDto): Observable<void> {
    return this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/news-article/${id}/reject`,
      body: input,
    }, { apiName: this.apiName });
  }

  archive(id: string, input: ReviewNewsArticleDto): Observable<void> {
    return this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/news-article/${id}/archive`,
      body: input,
    }, { apiName: this.apiName });
  }

  like(id: string): Observable<void> {
    return this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/news-article/${id}/like`,
    }, { apiName: this.apiName });
  }

  getApprovedComments(articleId: string): Observable<NewsCommentDto[]> {
    return this.restService.request<any, NewsCommentDto[]>({
      method: 'GET',
      url: `/api/app/news-comment/approved-list-by-article/${articleId}`,
    }, { apiName: this.apiName });
  }

  getCommentList(input: PagedNewsCommentRequestDto): Observable<PagedResultDto<NewsCommentDto>> {
    return this.restService.request<any, PagedResultDto<NewsCommentDto>>({
      method: 'GET',
      url: '/api/app/news-comment',
      params: input,
    }, { apiName: this.apiName });
  }

  createComment(input: CreateNewsCommentDto): Observable<NewsCommentDto> {
    return this.restService.request<any, NewsCommentDto>({
      method: 'POST',
      url: '/api/app/news-comment',
      body: input,
    }, { apiName: this.apiName });
  }

  reviewComment(id: string, status: NewsCommentStatus): Observable<NewsCommentDto> {
    return this.restService.request<any, NewsCommentDto>({
      method: 'POST',
      url: `/api/app/news-comment/${id}/review`,
      body: { status },
    }, { apiName: this.apiName });
  }

  deleteComment(id: string): Observable<void> {
    return this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/news-comment/${id}`,
    }, { apiName: this.apiName });
  }

  importArticles(file: Blob, fileName: string): Observable<NewsImportResultDto> {
    const formData = new FormData();
    formData.append('file', file, fileName);
    return this.restService.request<any, NewsImportResultDto>({
      method: 'POST',
      url: '/api/app/news-import/import',
      body: formData,
    }, { apiName: this.apiName });
  }

  /** 下载资讯导入 Excel 模板（P1-13 修复） */
  downloadImportTemplate(): Observable<Blob> {
    return this.restService.request<any, Blob>({
      method: 'GET',
      url: '/api/app/news-import/download-template',
      responseType: 'blob',
    }, { apiName: this.apiName });
  }
}

export interface NewsImportResultDto {
  totalCount: number;
  successCount: number;
  failCount: number;
  failItems: NewsImportFailItemDto[];
}

export interface NewsImportFailItemDto {
  rowNumber: number;
  title: string;
  reason: string;
}
