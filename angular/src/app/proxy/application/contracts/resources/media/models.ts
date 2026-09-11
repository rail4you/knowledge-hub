import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { ResourceMediaJobStatus } from '../../../../resources/media/resource-media-job-status.enum';
import type { ResourceArtifactKind } from '../../../../resources/media/resource-artifact-kind.enum';
import type { ResourceArtifactState } from '../../../../resources/media/resource-artifact-state.enum';

export interface GetResourceMediaJobsInput extends PagedAndSortedResultRequestDto {
  resourceId?: string | null;
  status?: ResourceMediaJobStatus | null;
  filter?: string | null;
}

export interface ResourceArtifactDto {
  kind?: ResourceArtifactKind;
  variant?: string;
  filePath?: string;
  state?: ResourceArtifactState;
  errorMessage?: string | null;
  sizeBytes?: number | null;
  generatedAt?: string;
}

export interface ResourceMediaJobDto extends EntityDto<string> {
  resourceId?: string;
  resourceName?: string | null;
  resourceVersionId?: string | null;
  status?: ResourceMediaJobStatus;
  progress?: number;
  progressMessage?: string | null;
  errorMessage?: string | null;
  retryCount?: number;
  startedAt?: string | null;
  completedAt?: string | null;
  creationTime?: string;
  artifacts?: ResourceArtifactDto[];
}
