import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { HybridSearchQueryDto, IndexDocumentDto, IndexStatusDto, IndexTaskResultDto, LogViewDto, PopularSearchDto, SearchHistoryDto, SearchQueryDto, SearchResultDto, SearchStatsDto, TopResourceDto } from '../contracts/search/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class SearchService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  deleteIndex = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/search/index/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getIndexTaskStatus = (taskId: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexStatusDto>({
      method: 'GET',
      url: `/api/app/search/index-task-status/${taskId}`,
    },
    { apiName: this.apiName,...config });
  

  getIndexingTasks = (skipCount?: number, maxResultCount: number = 20, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexStatusDto[]>({
      method: 'GET',
      url: '/api/app/search/indexing-tasks',
      params: { skipCount, maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMySearchHistory = (skipCount?: number, maxResultCount: number = 20, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SearchHistoryDto[]>({
      method: 'GET',
      url: '/api/app/search/my-search-history',
      params: { skipCount, maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPopularSearches = (count: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PopularSearchDto[]>({
      method: 'GET',
      url: '/api/app/search/popular-searches',
      params: { count },
    },
    { apiName: this.apiName,...config });
  

  getSearchStats = (startDate?: string, endDate?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SearchStatsDto>({
      method: 'GET',
      url: '/api/app/search/search-stats',
      params: { startDate, endDate },
    },
    { apiName: this.apiName,...config });
  

  getTopResources = (count: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TopResourceDto[]>({
      method: 'GET',
      url: '/api/app/search/top-resources',
      params: { count },
    },
    { apiName: this.apiName,...config });
  

  hybridSearch = (input: HybridSearchQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SearchResultDto>({
      method: 'POST',
      url: '/api/app/search/hybrid-search',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  indexResource = (input: IndexDocumentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexTaskResultDto>({
      method: 'POST',
      url: '/api/app/search/index-resource',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  logView = (input: LogViewDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/search/log-view',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  search = (input: SearchQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SearchResultDto>({
      method: 'POST',
      url: '/api/app/search/search',
      body: input,
    },
    { apiName: this.apiName,...config });
}