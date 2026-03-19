import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LogViewDto, PopularSearchDto, SearchHistoryDto, SearchStatsDto, TopResourceDto } from '../contracts/search/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class SearchAnalyticsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getPopularSearches = (count: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PopularSearchDto[]>({
      method: 'GET',
      url: '/api/app/search-analytics/popular-searches',
      params: { count },
    },
    { apiName: this.apiName,...config });
  

  getSearchStats = (startDate: string, endDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SearchStatsDto>({
      method: 'GET',
      url: '/api/app/search-analytics/search-stats',
      params: { startDate, endDate },
    },
    { apiName: this.apiName,...config });
  

  getTopResources = (count: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TopResourceDto[]>({
      method: 'GET',
      url: '/api/app/search-analytics/top-resources',
      params: { count },
    },
    { apiName: this.apiName,...config });
  

  getUserSearchHistory = (userId: string, skipCount?: number, maxResultCount: number = 20, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SearchHistoryDto[]>({
      method: 'GET',
      url: `/api/app/search-analytics/user-search-history/${userId}`,
      params: { skipCount, maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  logResourceView = (input: LogViewDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/search-analytics/log-resource-view',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  logSearch = (userId: string, query: string, searchType: number, resultCount: number, filters: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/search-analytics/log-search/${userId}`,
      params: { query, searchType, resultCount, filters },
    },
    { apiName: this.apiName,...config });
}