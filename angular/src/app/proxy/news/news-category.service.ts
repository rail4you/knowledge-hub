import type { CreateUpdateNewsCategoryDto, NewsCategoryDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class NewsCategoryService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateUpdateNewsCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsCategoryDto>({
      method: 'POST',
      url: '/api/app/news-category',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/news-category/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsCategoryDto>({
      method: 'GET',
      url: `/api/app/news-category/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getActiveList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsCategoryDto[]>({
      method: 'GET',
      url: '/api/app/news-category/active-list',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<NewsCategoryDto>>({
      method: 'GET',
      url: '/api/app/news-category',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTree = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsCategoryDto[]>({
      method: 'GET',
      url: '/api/app/news-category/tree',
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateNewsCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsCategoryDto>({
      method: 'PUT',
      url: `/api/app/news-category/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}