import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RestService } from '@abp/ng.core';

export interface SearchQueryDto {
  query?: string;
  fileExtensions?: string[];
  categoryId?: string;
  startDate?: string;
  endDate?: string;
  skipCount: number;
  maxResultCount: number;
  sorting: string;
  indexName?: string;
  /** 学生端传 "2,3" 限制仅已审核资源 */
  statusFilter?: string;
}

export interface HybridSearchQueryDto extends SearchQueryDto {
  queryEmbedding?: number[];
}

export interface DocumentSearchResultDto {
  id: string;
  resourceId: string;
  resourceName: string;
  pageNumber: number;
  content: string;
  highlightedContent: string | null;
  title: string | null;
  relevanceScore: number;
  fileExtension: string;
  resourceType: number;
  categoryName?: string;
  uploadDate: string;
  sourceType: 'document' | 'video';
  /** 该资源命中的页数/片段数（>1 时列表会提示“还有 N 处命中”） */
  matchCount?: number;
  videoId?: string;
  videoName?: string;
  videoUrl?: string;
  startTime?: string;
  endTime?: string;
  eventDescription?: string;
  /** AI 生成的资源摘要 */
  summary?: string | null;
  /** 资源描述 */
  description?: string | null;
  /** 关键词（逗号分隔） */
  keywords?: string | null;
  /** 文件大小（字节） */
  fileSize?: number | null;
  /** 版权信息 */
  copyrightInfo?: string | null;
  /** 分类 Id */
  categoryId?: string | null;
  /** 分类名称 */
  categoryTitle?: string | null;
  /** 上传者名称 */
  creatorName?: string | null;
  /** 当前版本号 */
  currentVersion?: number | null;
  /** 资源状态（枚举名） */
  statusName?: string | null;
  viewCount?: number;
  downloadCount?: number;
  collectionCount?: number;
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
    return this.restService.request({ method: 'POST', url: '/api/app/search/search', body: query },
      { apiName: 'KnowledgeHub' });
  }

  /** 按资源 Id 取该资源在搜索索引中的最佳命中（详情页直接通过 URL 打开时使用） */
  getResourceResult(resourceId: string): Observable<DocumentSearchResultDto | null> {
    return this.restService.request(
      { method: 'GET', url: `/api/app/search/resource-result/${resourceId}` },
      { apiName: 'Search' });
  }

  searchDocuments(query: { query: string; limit?: number; offset?: number }): Observable<any> {
    return this.restService.request({ method: 'POST', url: `${this.apiUrl}/search-documents`, body: query }, { apiName: 'Resources' });
  }

  hybridSearch(query: HybridSearchQueryDto): Observable<SearchResultDto> {
    return this.restService.request({ method: 'POST', url: '/api/app/search/hybrid-search', body: query }, { apiName: 'Search' });
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
    return this.restService.request({ method: 'POST', url: '/api/app/search/log-view', body: input }, { apiName: 'Search' });
  }

  getMySearchHistory(skipCount = 0, maxResultCount = 20): Observable<PagedResultDto<SearchHistoryDto>> {
    return this.restService.request({ method: 'GET', url: '/api/app/search/my-search-history', params: { skipCount, maxResultCount } }, { apiName: 'Default' });
  }

  deleteMySearchHistory(id: string): Observable<void> {
    return this.restService.request({ method: 'DELETE', url: `/api/app/search/${id}/my-search-history` }, { apiName: 'Default' });
  }

  clearMySearchHistory(): Observable<void> {
    return this.restService.request({ method: 'POST', url: '/api/app/search/clear-my-search-history' }, { apiName: 'Default' });
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
