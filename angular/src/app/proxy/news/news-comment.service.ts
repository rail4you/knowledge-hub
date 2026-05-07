import type { CreateNewsCommentDto, NewsCommentDto, PagedNewsCommentRequestDto, ReviewNewsCommentDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class NewsCommentService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateNewsCommentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsCommentDto>({
      method: 'POST',
      url: '/api/app/news-comment',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/news-comment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getApprovedListByArticle = (articleId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsCommentDto[]>({
      method: 'GET',
      url: `/api/app/news-comment/approved-list-by-article/${articleId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedNewsCommentRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<NewsCommentDto>>({
      method: 'GET',
      url: '/api/app/news-comment',
      params: { articleId: input.articleId, status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  review = (id: string, input: ReviewNewsCommentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsCommentDto>({
      method: 'POST',
      url: `/api/app/news-comment/${id}/review`,
      body: input,
    },
    { apiName: this.apiName,...config });
}