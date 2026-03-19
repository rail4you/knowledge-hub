import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { HybridSearchQueryDto, IndexStatusDto, IndexTaskResultDto, SearchQueryDto, SearchResultDto } from '../contracts/search/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class MeiliSearchService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  deleteDocument = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/meili-search/document/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  ensureIndexExists = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/meili-search/ensure-index-exists',
    },
    { apiName: this.apiName,...config });
  

  getAllIndexingTasks = (skipCount?: number, maxResultCount: number = 20, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexStatusDto[]>({
      method: 'GET',
      url: '/api/app/meili-search/indexing-tasks',
      params: { skipCount, maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getIndexingTaskStatus = (taskId: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexStatusDto>({
      method: 'GET',
      url: `/api/app/meili-search/indexing-task-status/${taskId}`,
    },
    { apiName: this.apiName,...config });
  

  getPendingIndexingTasks = (skipCount?: number, maxResultCount: number = 20, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexStatusDto[]>({
      method: 'GET',
      url: '/api/app/meili-search/pending-indexing-tasks',
      params: { skipCount, maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  hybridSearch = (query: HybridSearchQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SearchResultDto>({
      method: 'POST',
      url: '/api/app/meili-search/hybrid-search',
      body: query,
    },
    { apiName: this.apiName,...config });
  

  indexAllPages = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexTaskResultDto>({
      method: 'POST',
      url: `/api/app/meili-search/index-all-pages/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  indexDocument = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexTaskResultDto>({
      method: 'POST',
      url: `/api/app/meili-search/index-document/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  retryFailedIndexing = (documentIndexId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/meili-search/retry-failed-indexing/${documentIndexId}`,
    },
    { apiName: this.apiName,...config });
  

  search = (query: SearchQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SearchResultDto>({
      method: 'POST',
      url: '/api/app/meili-search/search',
      body: query,
    },
    { apiName: this.apiName,...config });
}