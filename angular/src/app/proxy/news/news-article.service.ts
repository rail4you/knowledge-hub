import type { CreateUpdateNewsArticleDto, NewsArticleDto, PagedNewsArticleRequestDto, ReviewNewsArticleDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class NewsArticleService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  archive = (id: string, input: ReviewNewsArticleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/news-article/${id}/archive`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateNewsArticleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsArticleDto>({
      method: 'POST',
      url: '/api/app/news-article',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/news-article/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsArticleDto>({
      method: 'GET',
      url: `/api/app/news-article/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getHotList = (maxCount: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsArticleDto[]>({
      method: 'GET',
      url: '/api/app/news-article/hot-list',
      params: { maxCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedNewsArticleRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<NewsArticleDto>>({
      method: 'GET',
      url: '/api/app/news-article',
      params: { filter: input.filter, categoryId: input.categoryId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPublishedList = (input: PagedNewsArticleRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<NewsArticleDto>>({
      method: 'GET',
      url: '/api/app/news-article/published-list',
      params: { filter: input.filter, categoryId: input.categoryId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  like = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/news-article/${id}/like`,
    },
    { apiName: this.apiName,...config });
  

  publish = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/news-article/${id}/publish`,
    },
    { apiName: this.apiName,...config });
  

  reject = (id: string, input: ReviewNewsArticleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/news-article/${id}/reject`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submitForReview = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/news-article/${id}/submit-for-review`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateNewsArticleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsArticleDto>({
      method: 'PUT',
      url: `/api/app/news-article/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}