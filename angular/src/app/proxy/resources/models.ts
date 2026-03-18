export interface ResourceDto {
  id?: string;
  name?: string;
  description?: string;
  resourceType?: number;
  categoryId?: string;
  categoryName?: string;
  filePath?: string;
  fileSize?: number;
  fileExtension?: string;
  originalFileName?: string;
  status?: number;
  currentVersion?: number;
  keywords?: string;
  copyrightInfo?: string;
  isDownloadable?: boolean;
  collectionCount?: number;
  downloadCount?: number;
  viewCount?: number;
  organizationId?: string;
  organizationName?: string;
  creatorId?: string;
  creatorName?: string;
  creationTime?: string;
}

export interface ResourceVersionDto {
  id?: string;
  resourceId?: string;
  version?: number;
  filePath?: string;
  fileSize?: number;
  updateContent?: string;
  isCurrentVersion?: boolean;
  creationTime?: string;
  creatorId?: string;
  creatorName?: string;
}

export interface ResourceCategoryDto {
  id?: string;
  name?: string;
  parentId?: string;
  parentName?: string;
  code?: string;
  sortOrder?: number;
  isActive?: boolean;
  children?: ResourceCategoryDto[];
  creationTime?: string;
}

export interface ResourceAuditDto {
  id?: string;
  resourceId?: string;
  auditType?: number;
  status?: number;
  comment?: string;
  auditorId?: string;
  auditorName?: string;
  creationTime?: string;
}

export interface CreateUpdateResourceDto {
  name?: string;
  description?: string;
  resourceType?: number;
  categoryId?: string;
  keywords?: string;
  copyrightInfo?: string;
  isDownloadable?: boolean;
  organizationId?: string;
}

export interface CreateUpdateResourceCategoryDto {
  name?: string;
  parentId?: string;
  code?: string;
  sortOrder?: number;
  isActive?: boolean;
}

export interface AuditResourceDto {
  resourceId?: string;
  auditType?: number;
  status?: number;
  comment?: string;
}

export interface UploadVersionDto {
  resourceId?: string;
  updateContent?: string;
}

export interface ResourceListQueryDto {
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
  filter?: string;
  status?: number;
  resourceType?: number;
  categoryId?: string;
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

export interface UploadChunkDto {
  uploadId?: string;
  fileName?: string;
  chunkNumber?: number;
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

export interface PhysicalDeleteRequestDto {
  id?: string;
  resourceId?: string;
  resourceName?: string;
  reason?: string;
  status?: number;
  requesterId?: string;
  requesterName?: string;
  approverId?: string;
  approverName?: string;
  approvalTime?: string;
  creationTime?: string;
}

export interface CreatePhysicalDeleteRequestDto {
  resourceId?: string;
  reason?: string;
}
