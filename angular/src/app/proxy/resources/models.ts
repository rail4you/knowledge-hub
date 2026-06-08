import type { AuditStatus } from './enums/audit-status.enum';
import type { ResourceType } from './enums/resource-type.enum';
import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AuditType } from './enums/audit-type.enum';
import type { ResourceStatus } from './enums/resource-status.enum';

export interface AuditResourceDto {
  resourceId?: string;
  status?: AuditStatus;
  comment?: string | null;
}

export interface CompleteUploadDto {
  uploadId?: string;
  fileName?: string;
  totalChunks?: number;
}

export interface CompleteUploadResultDto {
  filePath?: string;
  fileSize?: number;
  fileExtension?: string;
  originalFileName?: string;
}

export interface CreatePhysicalDeleteRequestDto {
  resourceId?: string;
  reason?: string;
}

export interface CreateUpdateResourceCategoryDto {
  name?: string;
  parentId?: string | null;
  code?: string | null;
  sortOrder?: number;
  isActive?: boolean;
}

export interface CreateUpdateResourceDto {
  name?: string;
  description?: string | null;
  resourceType?: ResourceType;
  categoryId?: string | null;
  keywords?: string | null;
  copyrightInfo?: string | null;
  isDownloadable?: boolean;
  // P1-13：与后端 Resource.IsResume 对应。等待下次 abp generate-proxy 重新生成时同步。
  isResume?: boolean;
  organizationId?: string | null;
  filePath?: string | null;
  fileSize?: number | null;
  fileExtension?: string | null;
  originalFileName?: string | null;
}

export interface DocumentPageSearchResultDto {
  id?: string;
  resourceId?: string;
  resourceName?: string;
  pageNumber?: number;
  content?: string;
  highlightedContent?: string | null;
  title?: string | null;
  fileExtension?: string;
  resourceType?: number;
  categoryName?: string | null;
  uploadDate?: string;
  relevanceScore?: number;
  sourceType?: string;
  videoId?: string | null;
  videoName?: string | null;
  videoUrl?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  eventDescription?: string | null;
}

export interface InitiateUploadDto {
  fileName?: string;
  totalSize?: number;
  chunkSize?: number;
}

export interface InitiateUploadResultDto {
  uploadId?: string;
  chunkSize?: number;
  totalChunks?: number;
}

export interface MeiliSearchQueryDto {
  query?: string;
  limit?: number;
  offset?: number;
  resourceType?: number | null;
  categoryId?: string | null;
  indexName?: string;
}

export interface MeiliSearchResultDto {
  items?: DocumentPageSearchResultDto[];
  totalCount?: number;
  processingTimeMs?: number;
}

export interface PhysicalDeleteRequestDto extends EntityDto<string> {
  resourceId?: string;
  resourceName?: string;
  reason?: string;
  status?: number;
  requesterId?: string;
  requesterName?: string;
  approverId?: string | null;
  approverName?: string | null;
  approvalTime?: string | null;
  creationTime?: string;
}

export interface ResourceAuditDto extends EntityDto<string> {
  resourceId?: string;
  auditType?: AuditType;
  status?: AuditStatus;
  comment?: string | null;
  auditorId?: string;
  auditorName?: string | null;
  creationTime?: string;
}

export interface ResourceCategoryDto extends FullAuditedEntityDto<string> {
  name?: string;
  parentId?: string | null;
  parentName?: string | null;
  code?: string | null;
  sortOrder?: number;
  isActive?: boolean;
  children?: ResourceCategoryDto[];
}

export interface ResourceDto extends FullAuditedEntityDto<string> {
  name?: string;
  description?: string | null;
  resourceType?: ResourceType;
  categoryId?: string | null;
  categoryName?: string | null;
  filePath?: string;
  fileSize?: number;
  fileExtension?: string;
  originalFileName?: string;
  status?: ResourceStatus;
  currentVersion?: number;
  keywords?: string | null;
  copyrightInfo?: string | null;
  isDownloadable?: boolean;
  // P1-13：与后端 Resource.IsResume 对应。等待下次 abp generate-proxy 重新生成时同步。
  isResume?: boolean;
  collectionCount?: number;
  downloadCount?: number;
  viewCount?: number;
  organizationId?: string | null;
  organizationName?: string | null;
  creatorId?: string;
  creatorName?: string | null;
}

export interface ResourceListQueryDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  status?: ResourceStatus | null;
  resourceType?: ResourceType | null;
  categoryId?: string | null;
  startDate?: string | null;
  endDate?: string | null;
}

export interface ResourceSearchQueryDto extends PagedAndSortedResultRequestDto {
  query?: string | null;
  resourceType?: ResourceType | null;
  categoryId?: string | null;
  startDate?: string | null;
  endDate?: string | null;
}

export interface ResourceVersionDto extends EntityDto<string> {
  resourceId?: string;
  version?: number;
  filePath?: string;
  fileSize?: number;
  updateContent?: string | null;
  isCurrentVersion?: boolean;
  creationTime?: string;
  creatorId?: string;
  creatorName?: string | null;
}

export interface UploadChunkDto {
  uploadId?: string;
  fileName?: string;
  chunkNumber?: number;
  isLastChunk?: boolean;
}

export interface UploadVersionDto {
  resourceId?: string;
  updateContent?: string | null;
  filePath?: string | null;
  fileSize?: number | null;
  fileExtension?: string | null;
  originalFileName?: string | null;
}
