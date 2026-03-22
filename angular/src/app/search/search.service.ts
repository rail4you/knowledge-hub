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

export interface IndexingJobDto {
  id: string;
  resourceId: string;
  resourceName?: string;
  resourceVersionId?: string;
  status: IndexingJobStatus;
  progress: number;
  errorMessage?: string;
  totalPages?: number;
  processedPages?: number;
  startedAt?: string;
  completedAt?: string;
  retryCount: number;
  nextRetryAt?: string;
  creationTime: string;
}

export enum IndexingJobStatus {
  Pending = 0,
  Parsing = 10,
  Indexing = 20,
  Completed = 30,
  Failed = 40,
  Cancelled = 50
}

export interface GetIndexingJobsInput {
  resourceId?: string;
  status?: IndexingJobStatus;
  skipCount?: number;
  maxResultCount?: number;
}

export interface CreateIndexingJobInput {
  resourceId: string;
  resourceVersionId?: string;
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
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

  getIndexingJobs(input: GetIndexingJobsInput): Observable<PagedResultDto<IndexingJobDto>> {
    const params: Record<string, any> = {
      skipCount: input.skipCount ?? 0,
      maxResultCount: input.maxResultCount ?? 20
    };
    if (input.resourceId) params.resourceId = input.resourceId;
    if (input.status !== undefined) params.status = input.status;
    
    return this.restService.request({ 
      method: 'GET', 
      url: '/api/app/indexing-job', 
      params 
    }, { apiName: 'Search' });
  }

  getIndexingJob(id: string): Observable<IndexingJobDto> {
    return this.restService.request({ 
      method: 'GET', 
      url: `/api/app/indexing-job/${id}` 
    }, { apiName: 'Search' });
  }

  getIndexingJobByResourceId(resourceId: string): Observable<IndexingJobDto | null> {
    return this.restService.request({ 
      method: 'GET', 
      url: `/api/app/indexing-job/by-resource/${resourceId}` 
    }, { apiName: 'Search' });
  }

  createIndexingJob(input: CreateIndexingJobInput): Observable<IndexingJobDto> {
    return this.restService.request({ 
      method: 'POST', 
      url: '/api/app/indexing-job',
      body: input
    }, { apiName: 'Search' });
  }

  retryIndexingJob(id: string): Observable<void> {
    return this.restService.request({ 
      method: 'POST', 
      url: `/api/app/indexing-job/${id}/retry` 
    }, { apiName: 'Search' });
  }

  cancelIndexingJob(id: string): Observable<void> {
    return this.restService.request({ 
      method: 'POST', 
      url: `/api/app/indexing-job/${id}/cancel` 
    }, { apiName: 'Search' });
  }

  retryAllFailedIndexingJobs(): Observable<void> {
    return this.restService.request({ 
      method: 'POST', 
      url: '/api/app/indexing-job/retry-all-failed' 
    }, { apiName: 'Search' });
  }
}
