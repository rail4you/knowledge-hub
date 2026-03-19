import type { ResourceType } from '../../../../resources/enums/resource-type.enum';

export interface DocumentSearchResultDto {
  resourceId?: string;
  resourceName?: string;
  pageNumber?: number;
  pageContent?: string;
  pageTitle?: string | null;
  highlightedText?: string;
  previewText?: string;
  relevanceScore?: number;
  fileExtension?: string;
  resourceType?: ResourceType;
  categoryName?: string | null;
  uploadDate?: string;
}

export interface HybridSearchQueryDto extends SearchQueryDto {
  queryEmbedding?: number[] | null;
}

export interface IndexDocumentDto {
  resourceId?: string;
}

export interface IndexStatusDto {
  documentIndexId?: string;
  resourceId?: string;
  pageNumber?: number;
  status?: string;
  errorMessage?: string | null;
  creationTime?: string;
}

export interface IndexTaskResultDto {
  taskId?: number;
  documentIndexId?: string;
  status?: string;
}

export interface LogViewDto {
  resourceId?: string;
  pageNumber?: number | null;
  viewDurationSeconds?: number;
  viewSource?: number;
}

export interface PopularSearchDto {
  query?: string;
  count?: number;
}

export interface SearchHistoryDto {
  id?: string;
  queryText?: string;
  creationTime?: string;
  resultCount?: number;
}

export interface SearchQueryDto {
  query?: string;
  resourceTypes?: ResourceType[] | null;
  categoryId?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  fileExtension?: string | null;
  skipCount?: number;
  maxResultCount?: number;
  sorting?: string;
}

export interface SearchResultDto {
  items?: DocumentSearchResultDto[];
  totalCount?: number;
  query?: string;
  facets?: Record<string, Record<string, number>>;
}

export interface SearchStatsDto {
  totalSearches?: number;
  uniqueUsers?: number;
  avgResultsPerSearch?: number;
  dailyTrends?: SearchTrendDto[];
  topSearchTerm?: string | null;
}

export interface SearchTrendDto {
  date?: string;
  searchCount?: number;
}

export interface TopResourceDto {
  resourceId?: string;
  resourceName?: string;
  exposureCount?: number;
  clickCount?: number;
  clickRate?: number;
}
