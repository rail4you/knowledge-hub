import type { MicroMajorStatus } from '../enums/micro-major-status.enum';
import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { MicroMajorEnrollmentStatus } from '../enums/micro-major-enrollment-status.enum';
import type { MicroMajorCertificateStatus } from '../enums/micro-major-certificate-status.enum';

export interface CreateUpdateMicroMajorCourseDto {
  courseId?: string;
  sortOrder?: number;
  isCore?: boolean;
}

export interface CreateUpdateMicroMajorDto {
  title?: string;
  summary?: string | null;
  description?: string | null;
  coverImageUrl?: string | null;
  industryField?: string | null;
  collaborationUnit?: string | null;
  status?: MicroMajorStatus;
  requiredCompletionRate?: number;
  isCertificateEnabled?: boolean;
  courses?: CreateUpdateMicroMajorCourseDto[];
}

export interface GetMicroMajorEnrollmentsInput extends PagedAndSortedResultRequestDto {
  microMajorId?: string | null;
  studentId?: string | null;
  status?: MicroMajorEnrollmentStatus | null;
}

export interface MicroMajorCertificateDto extends FullAuditedEntityDto<string> {
  microMajorId?: string;
  microMajorTitle?: string | null;
  enrollmentId?: string;
  studentId?: string;
  studentName?: string | null;
  certificateNo?: string;
  verifyCode?: string;
  status?: MicroMajorCertificateStatus;
  issuedAt?: string;
}

export interface MicroMajorCourseDto extends EntityDto<string> {
  microMajorId?: string;
  courseId?: string;
  courseTitle?: string | null;
  courseCoverImageUrl?: string | null;
  majorId?: string | null;
  majorName?: string | null;
  semester?: string | null;
  sortOrder?: number;
  isCore?: boolean;
}

export interface MicroMajorDetailDto extends MicroMajorDto {
  courses?: MicroMajorCourseDto[];
}

export interface MicroMajorDto extends FullAuditedEntityDto<string> {
  title?: string;
  summary?: string | null;
  description?: string | null;
  coverImageUrl?: string | null;
  industryField?: string | null;
  collaborationUnit?: string | null;
  status?: MicroMajorStatus;
  requiredCompletionRate?: number;
  isCertificateEnabled?: boolean;
  courseCount?: number;
  enrollmentCount?: number;
  currentUserProgress?: number | null;
  isCurrentUserEnrolled?: boolean;
}

export interface MicroMajorEnrollmentDto extends FullAuditedEntityDto<string> {
  microMajorId?: string;
  microMajorTitle?: string | null;
  studentId?: string;
  studentName?: string | null;
  status?: MicroMajorEnrollmentStatus;
  progress?: number;
  enrolledAt?: string;
  completedAt?: string | null;
  certificateIssuedAt?: string | null;
}

export interface MicroMajorResourceDto {
  id?: string;
  microMajorId?: string;
  resourceId?: string;
  resourceName?: string;
  fileExtension?: string | null;
  downloadCount?: number;
  sortOrder?: number;
  description?: string | null;
}

export interface PagedMicroMajorRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  status?: MicroMajorStatus | null;
}
