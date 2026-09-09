import type { MicroMajorStatus } from '../enums/micro-major-status.enum';
import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { MicroMajorEnrollmentStatus } from '../enums/micro-major-enrollment-status.enum';
import type { MicroMajorCertificateStatus } from '../enums/micro-major-certificate-status.enum';

export interface CertificateTemplateLayerDto {
  id?: string;
  fieldType?: string;
  label?: string;
  customFieldName?: string | null;
  x?: number;
  y?: number;
  fontSize?: number;
  color?: string;
  fontWeight?: number;
  center?: boolean;
  fontFamily?: string | null;
}

export interface CreateUpdateMicroMajorCertificateTemplateDto {
  microMajorId?: string;
  name?: string;
  imageUrl?: string;
  sortOrder?: number;
  layers?: CertificateTemplateLayerDto[] | null;
}

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
  filter?: string | null;
}

export interface IssueCertificateDefaultsDto {
  enrollmentId?: string;
  microMajorId?: string;
  microMajorTitle?: string | null;
  studentName?: string | null;
  studentNo?: string | null;
  suggestedCertificateNo?: string;
  issueDate?: string;
}

export interface IssueCertificateInputDto {
  enrollmentId?: string;
  certificateTemplateId?: string | null;
  studentNo?: string | null;
  advisor?: string | null;
  issueDate?: string | null;
  validUntil?: string | null;
  certificateNo?: string | null;
  compositeImageUrl?: string | null;
}

export interface MicroMajorCertificateDto extends FullAuditedEntityDto<string> {
  microMajorId?: string;
  microMajorTitle?: string | null;
  enrollmentId?: string;
  studentId?: string;
  studentName?: string | null;
  certificateNo?: string;
  verifyCode?: string;
  certificateImageUrl?: string | null;
  status?: MicroMajorCertificateStatus;
  issuedAt?: string;
  studentNo?: string | null;
  advisor?: string | null;
  issueDate?: string | null;
  validUntil?: string | null;
  layers?: CertificateTemplateLayerDto[];
}

export interface MicroMajorCertificateTemplateDto extends FullAuditedEntityDto<string> {
  microMajorId?: string;
  name?: string;
  imageUrl?: string;
  sortOrder?: number;
  layers?: CertificateTemplateLayerDto[];
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
  certificateImageUrl?: string | null;
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

export interface MyMicroMajorDto extends MicroMajorDto {
  enrollmentId?: string;
  enrollmentStatus?: MicroMajorEnrollmentStatus;
  progress?: number;
  enrolledAt?: string;
  completedAt?: string | null;
  certificateIssuedAt?: string | null;
  certificateImageUrl?: string | null;
  courses?: MicroMajorCourseDto[];
}

export interface PagedMicroMajorRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  status?: MicroMajorStatus | null;
}
