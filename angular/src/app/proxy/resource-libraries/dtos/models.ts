import type { LibraryContentType } from '../library-content-type.enum';
import type { ResourceLibraryVisibility } from '../resource-library-visibility.enum';
import type { ResourceType } from '../../resources/enums/resource-type.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { ResourceLibraryLearnerSource } from '../resource-library-learner-source.enum';
import type { ResourceLibraryStatus } from '../resource-library-status.enum';
import type { ResourceLibrarySharingScope } from '../resource-library-sharing-scope.enum';
import type { ResourceLibraryLearnerStatus } from '../resource-library-learner-status.enum';
import type { ResourceLibraryMemberRole } from '../resource-library-member-role.enum';

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
  openToExternalLearners?: boolean | null;
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

export interface GetResourceLibraryLearnersInput extends PagedAndSortedResultRequestDto {
  libraryId?: string | null;
  source?: ResourceLibraryLearnerSource | null;
  filter?: string | null;
}

export interface LibraryAttachOptionDto {
  id?: string;
  name?: string;
  visibility?: number;
  courseCount?: number;
}

export interface LibraryBriefDto {
  id?: string;
  name?: string;
}

export interface RegisterOtherLearnerInput {
  libraryId?: string;
  phone?: string;
  realName?: string;
  idCardNo?: string | null;
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
  openToExternalLearners?: boolean;
  formName?: string;
  sharingScope?: ResourceLibrarySharingScope;
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

export interface ResourceLibraryLearnerDto extends FullAuditedEntityDto<string> {
  libraryId?: string;
  libraryName?: string | null;
  userId?: string;
  displayName?: string;
  phone?: string | null;
  isPhoneVerified?: boolean;
  realName?: string | null;
  idCardMasked?: string | null;
  isRealNameVerified?: boolean;
  firstLearningAt?: string | null;
  enrolledAt?: string;
  status?: ResourceLibraryLearnerStatus;
  source?: ResourceLibraryLearnerSource;
  ownerTeacherId?: string | null;
  ownerTeacherName?: string | null;
  certificateCount?: number;
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

export interface ResourceLibraryPartnerTenantDto {
  id?: string;
  name?: string;
  tenantName?: string | null;
}

export interface ResourceLibraryTeacherDto {
  id?: string;
  libraryId?: string;
  userId?: string;
  userName?: string | null;
  tenantId?: string | null;
  tenantName?: string | null;
  isAssigned?: boolean;
  isImplicit?: boolean;
  assignedTime?: string;
}

export interface ResourceLibraryTeacherListRequestDto {
  filter?: string | null;
  libraryId?: string | null;
}

export interface TeachingLibraryCardDto {
  id?: string;
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  primaryMajorId?: string | null;
  primaryMajorName?: string | null;
  ownerTenantId?: string;
  ownerTenantName?: string | null;
  ownerSchoolLogoUrl?: string | null;
  principal?: string | null;
  visibility?: number;
  visibilityName?: string | null;
  memberCount?: number;
  partnerNames?: string[];
  courseCount?: number;
  resourceCount?: number;
  scope?: number;
}

export interface TeachingLibraryCatalogRequestDto extends PagedAndSortedResultRequestDto {
  scope?: string | null;
  filter?: string | null;
  primaryMajorId?: string | null;
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
  openToExternalLearners?: boolean | null;
  sortOrder?: number | null;
  status?: ResourceLibraryStatus | null;
}
