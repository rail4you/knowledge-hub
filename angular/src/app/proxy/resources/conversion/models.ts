import type { ResourceType } from '../enums/resource-type.enum';
import type { ResourceStatus } from '../enums/resource-status.enum';
import type { ResourceSourceType } from '../enums/resource-source-type.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface ConversionConcurrencyDto {
  serviceName?: string;
  maxConcurrent?: number;
}

export interface ConvertToCourseInput {
  resourceId?: string;
  libraryId?: string;
  courseId?: string;
  name?: string | null;
}

export interface ConvertToLibraryInput {
  resourceId?: string;
  sourceCourseId?: string | null;
  libraryId?: string;
  name?: string | null;
}

export interface CourseConvertibleResourceDto {
  resourceId?: string;
  name?: string;
  fileExtension?: string | null;
  fileSize?: number | null;
  resourceType?: ResourceType;
  status?: ResourceStatus;
  sourceType?: ResourceSourceType;
  sourceName?: string | null;
  creatorName?: string | null;
  creationTime?: string;
  libraryNames?: string[];
  sharedLibraryIds?: string[];
  isSharedToLibrary?: boolean;
}

export interface CourseResourceQueryDto extends PagedAndSortedResultRequestDto {
  courseId?: string;
  filter?: string | null;
}

export interface LibraryConvertibleResourceDto {
  resourceId?: string;
  name?: string;
  fileExtension?: string | null;
  fileSize?: number | null;
  resourceType?: ResourceType;
  status?: ResourceStatus;
  sourceType?: ResourceSourceType;
  sourceName?: string | null;
  creatorName?: string | null;
  creationTime?: string;
  ownerTenantName?: string | null;
}

export interface LibraryResourceQueryDto extends PagedAndSortedResultRequestDto {
  libraryId?: string;
  filter?: string | null;
}

export interface ResourceConversionResultDto {
  resourceId?: string;
  name?: string;
  message?: string;
}

export interface ResourceLibraryOptionDto {
  id?: string;
  name?: string;
  visibility?: number;
  isMine?: boolean;
}
