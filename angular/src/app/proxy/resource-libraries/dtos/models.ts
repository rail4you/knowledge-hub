import type { LibraryContentType } from '../library-content-type.enum';
import type { ResourceLibraryVisibility } from '../resource-library-visibility.enum';
import type { ResourceType } from '../../resources/enums/resource-type.enum';
import type { ResourceLibraryStatus } from '../resource-library-status.enum';
import type { ResourceLibraryMemberRole } from '../resource-library-member-role.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AddResourceLibraryMemberDto {
  memberTenantId?: string;
}

export interface AssignResourceLibraryTeacherDto {
  userId?: string;
}

export interface AttachLibraryContentDto {
  contentType?: LibraryContentType;
  contentId?: string;
}

export interface CreateResourceLibraryDto {
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  primaryMajorId?: string | null;
  principal?: string | null;
  visibility?: ResourceLibraryVisibility | null;
  partnerTenantIds?: string[];
  isPublic?: boolean | null;
  sortOrder?: number | null;
}

export interface CreateTeachingLibraryCourseDto {
  title?: string;
  description?: string | null;
  coverImageUrl?: string | null;
  majorId?: string | null;
  semester?: string | null;
  credits?: number | null;
  semesterHours?: number | null;
}

export interface CreateTeachingLibraryPracticumDto {
  title?: string;
  summary?: string | null;
  description?: string | null;
  coverImageUrl?: string | null;
  major?: string | null;
  className?: string | null;
  maxScore?: number | null;
}

export interface CreateTeachingLibraryResourceDto {
  name?: string;
  description?: string | null;
  resourceType?: ResourceType;
  filePath?: string | null;
  fileSize?: number | null;
  fileExtension?: string | null;
  originalFileName?: string | null;
  keywords?: string | null;
  copyrightInfo?: string | null;
  isDownloadable?: boolean | null;
  majorId?: string | null;
}

export interface ResourceLibraryDto {
  id?: string;
  tenantId?: string | null;
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  primaryMajorId?: string | null;
  primaryMajorName?: string | null;
  ownerTenantId?: string;
  ownerTenantName?: string | null;
  principal?: string | null;
  status?: ResourceLibraryStatus;
  visibility?: ResourceLibraryVisibility;
  isPublic?: boolean;
  sortOrder?: number;
  resourceCount?: number;
  courseCount?: number;
  practicumCount?: number;
  memberCount?: number;
  teacherCount?: number;
  currentRole?: number;
  isOwner?: boolean;
  isAssigned?: boolean;
  canView?: boolean;
  canContribute?: boolean;
  members?: ResourceLibraryMemberDto[];
}

export interface ResourceLibraryListRequestDto {
  primaryMajorId?: string | null;
  onlyJoined?: boolean | null;
  visibility?: ResourceLibraryVisibility | null;
}

export interface ResourceLibraryMemberDto {
  id?: string;
  memberTenantId?: string;
  memberTenantName?: string | null;
  role?: ResourceLibraryMemberRole;
  joinedTime?: string;
}

export interface ResourceLibraryTeacherDto {
  id?: string;
  libraryId?: string;
  userId?: string;
  userName?: string | null;
  tenantId?: string | null;
  tenantName?: string | null;
  isAssigned?: boolean;
  assignedTime?: string;
}

export interface ResourceLibraryTeacherListRequestDto {
  filter?: string | null;
  libraryId?: string | null;
}

export interface TeachingLibraryContentDto {
  id?: string;
  contentType?: LibraryContentType;
  contentTypeName?: string | null;
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  tenantId?: string | null;
  tenantName?: string | null;
  majorId?: string | null;
  majorName?: string | null;
  creatorId?: string | null;
  creatorName?: string | null;
  fileExtension?: string | null;
  fileSize?: number | null;
  status?: number | null;
  creationTime?: string;
  canDetach?: boolean;
}

export interface TeachingLibraryContentListRequestDto extends PagedAndSortedResultRequestDto {
  contentType?: LibraryContentType | null;
  filter?: string | null;
  onlyMine?: boolean | null;
}

export interface TeachingLibraryDetailDto extends TeachingLibraryDto {
  members?: ResourceLibraryMemberDto[];
  myRoleName?: string | null;
  creationTime?: string;
  lastModificationTime?: string | null;
}

export interface TeachingLibraryDto {
  id?: string;
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  visibility?: ResourceLibraryVisibility;
  visibilityName?: string | null;
  ownerTenantId?: string;
  ownerTenantName?: string | null;
  principal?: string | null;
  primaryMajorId?: string | null;
  primaryMajorName?: string | null;
  isOwner?: boolean;
  isPartner?: boolean;
  partnerSchoolNames?: string[];
  resourceCount?: number;
  courseCount?: number;
  practicumCount?: number;
  canContribute?: boolean;
}

export interface TeachingLibraryListRequestDto {
  filter?: string | null;
  visibility?: ResourceLibraryVisibility | null;
  onlyShared?: boolean | null;
}

export interface UpdateResourceLibraryDto {
  name?: string | null;
  description?: string | null;
  coverUrl?: string | null;
  primaryMajorId?: string | null;
  principal?: string | null;
  visibility?: ResourceLibraryVisibility | null;
  partnerTenantIds?: string[] | null;
  isPublic?: boolean | null;
  sortOrder?: number | null;
  status?: ResourceLibraryStatus | null;
}
