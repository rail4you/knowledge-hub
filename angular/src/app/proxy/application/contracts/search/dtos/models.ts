import type { ResourceType } from '../../../../resources/enums/resource-type.enum';
import type { EntityDto } from '@abp/ng.core';
import type { IndexingJobStatus } from '../../../../domain/search/indexing-job-status.enum';

export interface CreateResourceReviewDto {
  resourceId?: string;
  rating?: number;
  content?: string | null;
}

export interface DailySearchTrendDto {
  date?: string;
  totalSearchCount?: number;
  documentSearchCount?: number;
  videoSearchCount?: number;
  uniqueUsers?: number;
}

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
  sourceType?: string;
  videoId?: string | null;
  videoName?: string | null;
  videoUrl?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  eventDescription?: string | null;
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

export interface IndexingJobDto extends EntityDto<string> {
  resourceId?: string;
  resourceName?: string | null;
  resourceVersionId?: string | null;
  status?: IndexingJobStatus;
  progress?: number;
  errorMessage?: string | null;
  totalPages?: number | null;
  processedPages?: number | null;
  startedAt?: string | null;
  completedAt?: string | null;
  retryCount?: number;
  nextRetryAt?: string | null;
  creationTime?: string;
  jobType?: string;
  totalSegments?: number | null;
  processedSegments?: number | null;
}

export interface LogViewDto {
  resourceId?: string;
  pageNumber?: number | null;
  viewDurationSeconds?: number;
  viewSource?: number;
}

export interface MeiliDashboardDto {
  health?: MeiliHealthDto;
  version?: MeiliVersionDto;
  stats?: MeiliStatsDto;
  indexes?: MeiliIndexDto[];
  embedders?: Record<string, MeiliEmbedderDto>;
  recentTasks?: MeiliTaskDto[];
}

export interface MeiliDocumentGroupDto {
  resourceName?: string;
  resourceId?: string | null;
  fileExtension?: string | null;
  pageCount?: number;
  pages?: MeiliDocumentPageDto[];
  resourceType?: string;
  videoUrl?: string | null;
  uploadDate?: string | null;
}

export interface MeiliDocumentPageDto {
  id?: string;
  pageNumber?: number;
  pageTitle?: string | null;
  pageContent?: string | null;
  eventDescription?: string | null;
  startTime?: string | null;
  endTime?: string | null;
}

export interface MeiliEmbedderDto {
  source?: string;
  url?: string | null;
  model?: string | null;
  dimensions?: number | null;
  documentTemplate?: string | null;
}

export interface MeiliHealthDto {
  status?: string;
}

export interface MeiliIndexDto {
  uid?: string;
  primaryKey?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface MeiliIndexStatsDto {
  numberOfDocuments?: number;
  isIndexing?: boolean;
  fieldDistribution?: Record<string, number>;
}

export interface MeiliStatsDto {
  databaseSize?: number;
  usedDatabaseSize?: number;
  lastUpdate?: string | null;
  indexes?: Record<string, MeiliIndexStatsDto>;
}

export interface MeiliTaskDto {
  uid?: number;
  indexUid?: string | null;
  status?: string;
  type?: string;
  enqueuedAt?: string | null;
  startedAt?: string | null;
  finishedAt?: string | null;
}

export interface MeiliVersionDto {
  commitSha?: string;
  commitDate?: string;
  pkgVersion?: string;
}

export interface PageIndexSearchResultDto {
  resourceId?: string;
  resourceName?: string | null;
  nodeTitle?: string | null;
  nodeSummary?: string | null;
  nodeId?: string;
  startIndex?: number;
  endIndex?: number;
  docDescription?: string | null;
}

export interface PopularSearchDto {
  query?: string;
  count?: number;
}

export interface PopularSearchTermDto {
  keyword?: string;
  count?: number;
  sourceType?: string;
}

export interface RecommendedResourceDto {
  resourceId?: string;
  resourceName?: string;
  description?: string | null;
  resourceType?: number;
  categoryId?: string | null;
  categoryName?: string | null;
  keywords?: string | null;
  fileExtension?: string | null;
  fileSize?: number | null;
  viewCount?: number;
  collectionCount?: number;
  downloadCount?: number;
  averageRating?: number;
  totalReviews?: number;
  recommendationScore?: number;
  recommendationReason?: string;
  creationTime?: string;
}

export interface ResourcePageIndexDto {
  id?: string;
  resourceId?: string;
  resourceVersionId?: string;
  pageIndexJson?: string;
  sourceFormat?: string | null;
  model?: string | null;
  nodeCount?: number;
}

export interface ResourceRatingSummaryDto {
  resourceId?: string;
  averageRating?: number;
  totalReviews?: number;
  ratingDistribution?: number[];
  myReview?: ResourceReviewDto | null;
}

export interface ResourceReviewDto {
  id?: string;
  resourceId?: string;
  userId?: string;
  userName?: string;
  rating?: number;
  content?: string | null;
  creationTime?: string;
}

export interface ResourceStatisticsDto {
  resourceId?: string;
  totalViews?: number;
  uniqueViewers?: number;
  avgViewDurationSeconds?: number;
  totalDownloads?: number;
  totalCollections?: number;
  collectionRate?: number;
  downloadRate?: number;
  averageRating?: number;
  totalReviews?: number;
  ratingDistribution?: number[];
  viewsLast30Days?: number;
  viewsPrevious30Days?: number;
  viewTrendPercentage?: number;
  timesInSearchResults?: number;
  timesClickedFromSearch?: number;
  clickThroughRate?: number;
}

export interface SearchDashboardDto {
  all?: SearchStatsBreakdown;
  document?: SearchStatsBreakdown;
  video?: SearchStatsBreakdown;
  dailyTrends?: DailySearchTrendDto[];
  popularSearches?: PopularSearchTermDto[];
  topResources?: TopResourceStatsDto[];
  topRatedResources?: TopRatedResourceDto[];
}

export interface SearchHistoryDto {
  id?: string;
  queryText?: string;
  creationTime?: string;
  resultCount?: number;
}

export interface SearchQueryDto {
  query?: string;
  fileExtensions?: string[] | null;
  categoryId?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  skipCount?: number;
  maxResultCount?: number;
  sorting?: string;
  indexName?: string | null;
}

export interface SearchResultDto {
  items?: DocumentSearchResultDto[];
  totalCount?: number;
  query?: string;
  facets?: Record<string, Record<string, number>>;
}

export interface SearchStatsBreakdown {
  totalSearches?: number;
  todaySearches?: number;
  activeUsers?: number;
  todayActiveUsers?: number;
}

export interface SearchStatsDto {
  totalSearches?: number;
  uniqueUsers?: number;
  avgResultsPerSearch?: number;
  dailyTrends?: SearchTrendDto[];
  topSearchTerm?: string | null;
}

export interface SearchStatsQueryDto {
  startDate?: string | null;
  endDate?: string | null;
  tenantId?: string | null;
}

export interface SearchTrendDto {
  date?: string;
  searchCount?: number;
}

export interface TopRatedResourceDto {
  resourceId?: string;
  resourceName?: string;
  averageRating?: number;
  reviewCount?: number;
}

export interface TopResourceDto {
  resourceId?: string;
  resourceName?: string;
  exposureCount?: number;
  clickCount?: number;
  clickRate?: number;
}

export interface TopResourceStatsDto {
  resourceId?: string;
  resourceName?: string;
  searchCount?: number;
  clickCount?: number;
  clickRate?: number;
}

export interface UpdateResourceReviewDto {
  rating?: number;
  content?: string | null;
}

export interface VideoAnalysisRequestDto {
  filePath?: string | null;
  videoUrl?: string | null;
  customPrompt?: string | null;
}

export interface VideoAnalysisResultDto {
  rawContent?: string;
  events?: VideoTimelineEventDto[];
  usage?: VideoAnalysisUsageDto | null;
}

export interface VideoAnalysisUsageDto {
  promptTokens?: number;
  completionTokens?: number;
  totalTokens?: number;
}

export interface VideoTimelineEventDto {
  startTime?: string;
  endTime?: string;
  event?: string;
}
