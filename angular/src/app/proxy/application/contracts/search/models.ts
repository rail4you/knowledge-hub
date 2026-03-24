import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { IndexingJobStatus } from '../../../domain/search/indexing-job-status.enum';

export interface CreateIndexingJobInput {
  resourceId?: string;
  resourceVersionId?: string | null;
}

export interface GetIndexingJobsInput extends PagedAndSortedResultRequestDto {
  resourceId?: string | null;
  status?: IndexingJobStatus | null;
}

export interface PageContentDto {
  pageNumber?: number;
  content?: string;
  title?: string | null;
}

export interface TestParseResultDto {
  success?: boolean;
  errorMessage?: string | null;
  pageCount?: number;
  firstPagePreview?: string | null;
}
