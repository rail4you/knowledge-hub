import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { IndexingJobStatus } from '../../../domain/search/indexing-job-status.enum';

export interface CreateIndexingJobInput {
  resourceId?: string;
  resourceVersionId?: string | null;
}

export interface GetIndexingJobsInput extends PagedAndSortedResultRequestDto {
  resourceId?: string | null;
  status?: IndexingJobStatus | null;
  startTime?: string | null;
  endTime?: string | null;
}

export interface PageContentDto {
  pageNumber?: number;
  content?: string;
  title?: string | null;
}

export interface PageIndexSearchInput {
  query?: string;
  maxResults?: number;
}

export interface TestParseResultDto {
  success?: boolean;
  errorMessage?: string | null;
  pageCount?: number;
  firstPagePreview?: string | null;
}
