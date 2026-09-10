import type { CreationAuditedEntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { SpecialEduCategory } from '../special-edu-category.enum';
import type { SpecialEduPlanStatus } from '../special-edu-plan-status.enum';
import type { SpecialEduContentType } from '../special-edu-content-type.enum';

export interface GetIepListInputDto extends PagedAndSortedResultRequestDto {
  category?: SpecialEduCategory | null;
  status?: SpecialEduPlanStatus | null;
  studentUserId?: string | null;
  keyword?: string | null;
}

export interface GetSpecialEduStudentOptionsInput {
  courseId?: string | null;
}

export interface GetSpecialResourceListInputDto extends PagedAndSortedResultRequestDto {
  category?: SpecialEduCategory | null;
  modality?: string | null;
  excludeModality?: string | null;
  status?: SpecialEduPlanStatus | null;
  keyword?: string | null;
}

export interface GetTeachingDesignListInputDto extends PagedAndSortedResultRequestDto {
  category?: SpecialEduCategory | null;
  status?: SpecialEduPlanStatus | null;
  keyword?: string | null;
}

export interface IepPlanDto extends FullAuditedEntityDto<string> {
  studentUserId?: string;
  studentName?: string;
  category?: SpecialEduCategory;
  categoryName?: string;
  courseId?: string | null;
  courseTitle?: string | null;
  profileSummary?: string;
  longTermGoals?: string[];
  shortTermGoals?: string[];
  strategies?: string[];
  evaluation?: string[];
  homeSchool?: string[];
  legalBasis?: string;
  rawJson?: string;
  versionNumber?: number;
  parentVersionId?: string | null;
  status?: SpecialEduPlanStatus;
  reviewComment?: string | null;
  reviewerUserId?: string | null;
  reviewerName?: string | null;
}

export interface ResourcePairDto {
  text?: string;
  pinyin?: string;
  braille?: string;
  note?: string;
}

export interface ResourcePairInputDto {
  text?: string;
  pinyin?: string;
  braille?: string;
  note?: string;
}

export interface ReviewIepInputDto {
  id?: string;
  approved?: boolean;
  comment?: string | null;
}

export interface ReviewSpecialResourceInputDto {
  id?: string;
  approved?: boolean;
  comment?: string | null;
}

export interface ReviewTeachingDesignInputDto {
  id?: string;
  approved?: boolean;
  comment?: string | null;
}

export interface SaveIepInputDto {
  id?: string | null;
  studentUserId?: string;
  studentName?: string;
  category?: SpecialEduCategory;
  courseId?: string | null;
  resultJson?: string;
  sourceInputJson?: string;
}

export interface SaveSpecialResourceInputDto {
  id?: string | null;
  title?: string;
  category?: SpecialEduCategory;
  modality?: string;
  teachingDesignId?: string | null;
  iepPlanId?: string | null;
  courseId?: string | null;
  resultJson?: string;
  sourceInputJson?: string;
}

export interface SaveTeachingDesignInputDto {
  id?: string | null;
  category?: SpecialEduCategory;
  courseId?: string | null;
  resourceId?: string | null;
  resultJson?: string;
  sourceInputJson?: string;
}

export interface SeedMockDataInputDto {
  tenantId?: string | null;
}

export interface SetSpecialEduTenantEnabledDto {
  tenantId?: string;
  enabled?: boolean;
}

export interface SpecialEduContentVersionDto extends CreationAuditedEntityDto<string> {
  contentType?: SpecialEduContentType;
  entityId?: string;
  versionNumber?: number;
  title?: string;
  snapshotJson?: string;
  creatorName?: string | null;
}

export interface SpecialEduCourseOptionDto {
  id?: string;
  title?: string;
}

export interface SpecialEduResourceDto extends FullAuditedEntityDto<string> {
  title?: string;
  category?: SpecialEduCategory;
  categoryName?: string;
  modality?: string;
  modalityName?: string;
  teachingDesignId?: string | null;
  iepPlanId?: string | null;
  courseId?: string | null;
  contentText?: string;
  content?: string[];
  pairs?: ResourcePairDto[];
  rawJson?: string;
  versionNumber?: number;
  status?: SpecialEduPlanStatus;
  reviewComment?: string | null;
  reviewerUserId?: string | null;
  reviewerName?: string | null;
}

export interface SpecialEduStudentOptionDto {
  id?: string;
  userName?: string;
  name?: string;
  enrolledInSelectedCourse?: boolean;
}

export interface SpecialEduTeacherOptionDto {
  id?: string;
  userName?: string;
  name?: string;
  roleName?: string;
}

export interface SpecialEduTenantStateDto {
  tenantId?: string;
  tenantName?: string;
  enabled?: boolean;
}

export interface SpecialTeachingDesignDto extends FullAuditedEntityDto<string> {
  category?: SpecialEduCategory;
  categoryName?: string;
  courseId?: string | null;
  courseTitle?: string | null;
  resourceId?: string | null;
  title?: string;
  subject?: string;
  grade?: string;
  duration?: number;
  objectives?: string[];
  keyPoints?: string[];
  difficulties?: string[];
  sections?: TeachingSectionItemDto[];
  methods?: string[];
  resources?: string[];
  assessment?: string[];
  homework?: string[];
  boardDesign?: string[];
  slidesOutline?: string[];
  activities?: string[];
  assessmentTools?: string[];
  standardBasis?: string;
  rawJson?: string;
  versionNumber?: number;
  status?: SpecialEduPlanStatus;
  reviewComment?: string | null;
  reviewerUserId?: string | null;
  reviewerName?: string | null;
}

export interface SubmitIepForReviewInputDto {
  id?: string;
  reviewerUserId?: string | null;
}

export interface SubmitSpecialResourceForReviewInputDto {
  id?: string;
  reviewerUserId?: string | null;
}

export interface SubmitTeachingDesignForReviewInputDto {
  id?: string;
  reviewerUserId?: string | null;
}

export interface TeachingSectionInputDto {
  name?: string;
  duration?: number;
  content?: string;
}

export interface TeachingSectionItemDto {
  name?: string;
  duration?: number;
  content?: string;
  activities?: string[];
}

export interface UpdateIepContentDto {
  id?: string;
  profileSummary?: string;
  longTermGoals?: string[];
  shortTermGoals?: string[];
  strategies?: string[];
  evaluation?: string[];
  homeSchool?: string[];
  legalBasis?: string;
  resultJson?: string;
}

export interface UpdateResourceContentDto {
  id?: string;
  title?: string;
  content?: string[];
  pairs?: ResourcePairInputDto[];
  resultJson?: string;
}

export interface UpdateTeachingDesignContentDto {
  id?: string;
  title?: string;
  subject?: string;
  grade?: string;
  duration?: number;
  objectives?: string[];
  keyPoints?: string[];
  difficulties?: string[];
  sections?: TeachingSectionInputDto[];
  methods?: string[];
  resources?: string[];
  assessment?: string[];
  homework?: string[];
  boardDesign?: string[];
  slidesOutline?: string[];
  activities?: string[];
  assessmentTools?: string[];
  standardBasis?: string;
  resultJson?: string;
}
