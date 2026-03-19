import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RestService } from '@abp/ng.core';

export interface SearchQueryDto {
  query?: string;
  resourceTypes?: number[];
  categoryId?: string;
  startDate?: string;
  endDate?: string;
  fileExtension?: string;
  skipCount: number;
  maxResultCount: number;
  sorting: string;
}

export interface HybridSearchQueryDto extends SearchQueryDto {
  queryEmbedding?: number[];
}

export interface DocumentSearchResultDto {
  resourceId: string;
  resourceName: string;
  pageNumber: number;
  pageContent: string;
  pageTitle?: string;
  highlightedText: string;
  previewText: string;
  relevanceScore: number;
  fileExtension: string;
  resourceType: number;
  categoryName?: string;
  uploadDate: string;
}

export interface SearchResultDto {
  items: DocumentSearchResultDto[];
  totalCount: number;
  query: string;
  facets: Record<string, Record<string, number>>;
}

export interface IndexTaskResultDto {
  taskId: number;
  documentIndexId: string;
  status: string;
}

export interface IndexStatusDto {
  documentIndexId: string;
  resourceId: string;
  pageNumber: number;
  status: string;
  errorMessage?: string;
  creationTime: string;
}

export interface LogViewDto {
  resourceId: string;
  pageNumber?: number;
  viewDurationSeconds: number;
  viewSource: number;
}

export interface SearchHistoryDto {
  id: string;
  queryText: string;
  creationTime: string;
  resultCount: number;
}

export interface SearchStatsDto {
  totalSearches: number;
  uniqueUsers: number;
  avgResultsPerSearch: number;
  dailyTrends: SearchTrendDto[];
  topSearchTerm?: string;
}

export interface SearchTrendDto {
  date: string;
  searchCount: number;
}

export interface PopularSearchDto {
  query: string;
  count: number;
}

export interface TopResourceDto {
  resourceId: string;
  resourceName: string;
  exposureCount: number;
  clickCount: number;
  clickRate: number;
}

@Injectable({
  providedIn: 'root'
})
export class SearchService {
  private readonly restService = inject(RestService);
  private readonly apiUrl = '/api/app/resource';

  search(query: SearchQueryDto): Observable<SearchResultDto> {
    return this.restService.request({ method: 'POST', url: `${this.apiUrl}/search-documents`, body: query }, { apiName: 'Resources' });
  }

  searchDocuments(query: { query: string; limit?: number; offset?: number }): Observable<any> {
    return this.restService.request({ method: 'POST', url: `${this.apiUrl}/search-documents`, body: query }, { apiName: 'Resources' });
  }

  hybridSearch(query: HybridSearchQueryDto): Observable<SearchResultDto> {
    return this.restService.request({ method: 'POST', url: `${this.apiUrl}/search`, body: query }, { apiName: 'Resources' });
  }

  indexResource(resourceId: string): Observable<IndexTaskResultDto> {
    return this.restService.request({ method: 'POST', url: `${this.apiUrl}/index/${resourceId}` }, { apiName: 'Search' });
  }

  deleteIndex(resourceId: string): Observable<void> {
    return this.restService.request({ method: 'DELETE', url: `${this.apiUrl}/index/${resourceId}` }, { apiName: 'Search' });
  }

  getIndexingTasks(skipCount = 0, maxResultCount = 20): Observable<IndexStatusDto[]> {
    return this.restService.request({ method: 'GET', url: this.apiUrl, params: { skipCount, maxResultCount } }, { apiName: 'Search' });
  }

  getIndexTaskStatus(taskId: number): Observable<IndexStatusDto | null> {
    return this.restService.request({ method: 'GET', url: `${this.apiUrl}/index/status/${taskId}` }, { apiName: 'Search' });
  }

  logView(input: LogViewDto): Observable<void> {
    return this.restService.request({ method: 'POST', url: `${this.apiUrl}/analytics/view`, body: input }, { apiName: 'Search' });
  }

  getMySearchHistory(skipCount = 0, maxResultCount = 20): Observable<SearchHistoryDto[]> {
    return this.restService.request({ method: 'GET', url: `${this.apiUrl}/history`, params: { skipCount, maxResultCount } }, { apiName: 'Search' });
  }

  getSearchStats(startDate?: string, endDate?: string): Observable<SearchStatsDto> {
    return this.restService.request({ method: 'GET', url: `${this.apiUrl}/statistics`, params: { startDate, endDate } }, { apiName: 'Search' });
  }

  getPopularSearches(count = 10): Observable<PopularSearchDto[]> {
    return this.restService.request({ method: 'GET', url: `${this.apiUrl}/analytics/popular`, params: { count } }, { apiName: 'Search' });
  }

  getTopResources(count = 10): Observable<TopResourceDto[]> {
    return this.restService.request({ method: 'GET', url: `${this.apiUrl}/analytics/top-resources`, params: { count } }, { apiName: 'Search' });
  }
}
