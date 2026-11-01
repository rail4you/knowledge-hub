import type { LibraryContentType } from '../library-content-type.enum';
import type { BuildTaskDeliveryStatus } from '../build-task-delivery-status.enum';
import type { BuildTaskStatus } from '../build-task-status.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto, PagedResultRequestDto } from '@abp/ng.core';
import type { CourseSharedRefStatus } from '../course-shared-ref-status.enum';
import type { ResourceLibraryVisibility } from '../resource-library-visibility.enum';
import type { ResourceType } from '../../resources/enums/resource-type.enum';
import type { ResourceLibraryLearnerSource } from '../resource-library-learner-source.enum';
import type { LibraryTemplateStatus } from '../library-template-status.enum';
import type { ResourceLibraryStatus } from '../resource-library-status.enum';
import type { ResourceLibrarySharingScope } from '../resource-library-sharing-scope.enum';
import type { ResourceLibraryLearnerStatus } from '../resource-library-learner-status.enum';
import type { ResourceLibraryMemberRole } from '../resource-library-member-role.enum';
import type { ResourcePublicationStatus } from '../resource-publication-status.enum';

export interface AddResourceLibraryMemberDto {
  memberTenantId?: string;
}

export interface AssignResourceLibraryTeacherDto {
  userId?: string;
}

export interface AttachLibraryContentDto {
  contentType?: LibraryContentType;
  contentId?: string;
  contentVersionId?: string | null;
}

export interface BuildTaskDeliveryDto {
  id?: string;
  taskId?: string;
  taskItemId?: string;
  itemName?: string;
  contentType?: LibraryContentType;
  contentId?: string;
  contentName?: string | null;
  publicationId?: string | null;
  submittedBy?: string;
  submitterName?: string | null;
  status?: BuildTaskDeliveryStatus;
  statusName?: string;
  comment?: string | null;
  creationTime?: string;
}

export interface BuildTaskDto {
  id?: string;
  libraryId?: string;
  libraryName?: string | null;
  courseId?: string | null;
  title?: string;
  ownerSchoolId?: string;
  ownerSchoolName?: string | null;
  ownerUserId?: string | null;
  ownerUserName?: string | null;
  reviewerUserId?: string | null;
  dueAt?: string | null;
  requirements?: string | null;
  status?: BuildTaskStatus;
  statusName?: string;
  requiredTotal?: number;
  acceptedTotal?: number;
  submittedPending?: number;
  progressPercent?: number;
  isOverdue?: boolean;
  items?: BuildTaskItemDto[];
  deliveries?: BuildTaskDeliveryDto[];
  canSubmit?: boolean;
  canReview?: boolean;
}

export interface BuildTaskItemDto {
  id?: string;
  itemName?: string;
  contentType?: LibraryContentType;
  contentTypeName?: string;
  requiredCount?: number;
  acceptedCount?: number;
  submittedCount?: number;
  acceptance?: string | null;
}

export interface BuildTaskListRequestDto extends PagedAndSortedResultRequestDto {
  libraryId?: string | null;
  status?: BuildTaskStatus | null;
  onlyMine?: boolean;
  filter?: string | null;
}

export interface BuildTaskTemplateDto {
  id?: string;
  libraryId?: string | null;
  code?: string;
  name?: string;
  courseId?: string | null;
  isActive?: boolean;
  items?: BuildTaskTemplateItemDto[];
}

export interface BuildTaskTemplateItemDto {
  id?: string;
  itemName?: string;
  contentType?: LibraryContentType;
  contentTypeName?: string;
  requiredCount?: number;
  acceptance?: string | null;
  sortOrder?: number;
}

export interface CourseSharedRefAccessDto {
  canAccess?: boolean;
  reason?: string | null;
  resourceId?: string | null;
  libraryId?: string | null;
  sourceTenantId?: string | null;
}

export interface CourseSharedRefDto {
  id?: string;
  targetTenantId?: string;
  courseId?: string;
  courseTitle?: string | null;
  chapterId?: string | null;
  libraryId?: string;
  libraryName?: string | null;
  contentType?: LibraryContentType;
  contentId?: string;
  contentName?: string | null;
  contentVersionId?: string | null;
  sourceTenantId?: string;
  sourceTenantName?: string | null;
  status?: CourseSharedRefStatus;
  statusName?: string;
  revokedReason?: string | null;
  creationTime?: string;
  canManage?: boolean;
}

export interface CourseSharedRefListRequestDto extends PagedResultRequestDto {
  courseId?: string | null;
  status?: CourseSharedRefStatus | null;
  direction?: string | null;
}

export interface CreateBuildTaskDto {
  libraryId?: string;
  templateId?: string | null;
  courseId?: string | null;
  title?: string;
  ownerSchoolId?: string | null;
  ownerUserId?: string | null;
  reviewerUserId?: string | null;
  dueAt?: string | null;
  requirements?: string | null;
  collaboratorSchoolIds?: string[];
  items?: CreateBuildTaskTemplateItemDto[];
}

export interface CreateBuildTaskTemplateDto {
  libraryId?: string | null;
  code?: string;
  name?: string;
  courseId?: string | null;
  items?: CreateBuildTaskTemplateItemDto[];
}

export interface CreateBuildTaskTemplateItemDto {
  itemName?: string;
  contentType?: LibraryContentType;
  requiredCount?: number;
  acceptance?: string | null;
  sortOrder?: number;
}

export interface CreateCourseSharedRefDto {
  courseId?: string;
  chapterId?: string | null;
  libraryId?: string;
  contentId?: string;
  contentVersionId?: string | null;
}

export interface CreateLibraryTemplateDto {
  code?: string;
  name?: string;
  description?: string | null;
  configJson?: string;
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
  templateId?: string | null;
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

export interface LibraryTemplateDto {
  id?: string;
  code?: string;
  name?: string;
  version?: number;
  description?: string | null;
  configJson?: string;
  status?: LibraryTemplateStatus;
  statusName?: string;
}

export interface RegisterOtherLearnerInput {
  libraryId?: string;
  phone?: string;
  realName?: string;
  idCardNo?: string | null;
}

export interface ReportUsageEventDto {
  eventId?: string;
  courseId?: string | null;
  refId?: string | null;
  resourceId?: string | null;
  type?: string;
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

export interface ReviewBuildTaskDeliveryDto {
  deliveryId?: string;
  accept?: boolean;
  comment?: string | null;
}

export interface SharedRefStatisticsDto {
  referencedSchoolCount?: number;
  referencedCourseCount?: number;
  activeRefCount?: number;
  revokedRefCount?: number;
  learnerCount?: number;
}

export interface SubmitBuildTaskDeliveryDto {
  taskId?: string;
  taskItemId?: string;
  contentType?: LibraryContentType;
  contentId?: string;
  comment?: string | null;
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
  publicationStatus?: ResourcePublicationStatus | null;
  publicationStatusName?: string | null;
  publicationRejectReason?: string | null;
  reviewTicketId?: string | null;
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

export interface UpgradeCourseSharedRefDto {
  contentVersionId?: string | null;
}
