import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { CertificateApplicationStatus } from '../certificate-application-status.enum';
import type { CertificateStatus } from '../certificate-status.enum';

export interface ApplyCertificateInput {
  courseId?: string;
  templateId?: string | null;
}

export interface ApproveCertificateInput {
  applicationId?: string;
  templateId?: string | null;
  certificateNo?: string | null;
  verifyCode?: string | null;
  certificateImageUrl?: string | null;
  issueDate?: string | null;
  validUntil?: string | null;
  reviewRemark?: string | null;
}

export interface CertificateApplicationDto extends FullAuditedEntityDto<string> {
  libraryId?: string;
  libraryName?: string | null;
  courseId?: string;
  courseTitle?: string;
  studentId?: string;
  applicantName?: string;
  phoneMasked?: string | null;
  schoolName?: string | null;
  isLearningCompleted?: boolean;
  isAssessmentPassed?: boolean;
  isRealNameVerified?: boolean;
  status?: CertificateApplicationStatus;
  appliedAt?: string;
  reviewedAt?: string | null;
  reviewerName?: string | null;
  reviewRemark?: string | null;
  certificateId?: string | null;
  certificate?: CertificateDto | null;
}

export interface CertificateApplyEligibilityDto {
  courseId?: string;
  courseTitle?: string;
  libraryId?: string | null;
  libraryName?: string | null;
  isLearningCompleted?: boolean;
  isAssessmentPassed?: boolean;
  isRealNameVerified?: boolean;
  hasApplied?: boolean;
  canApply?: boolean;
  reason?: string | null;
}

export interface CertificateDto extends FullAuditedEntityDto<string> {
  templateId?: string;
  applicationId?: string | null;
  libraryId?: string;
  libraryName?: string | null;
  courseId?: string;
  courseTitle?: string;
  studentId?: string;
  studentName?: string;
  schoolName?: string | null;
  certificateNo?: string;
  verifyCode?: string;
  imageUrl?: string | null;
  issueDate?: string;
  issuedAt?: string;
  validUntil?: string | null;
  status?: CertificateStatus;
  isArchived?: boolean;
}

export interface CertificatePrepareDto {
  applicationId?: string;
  certificateNo?: string;
  verifyCode?: string;
}

export interface CertificateTemplateDto extends FullAuditedEntityDto<string> {
  name?: string;
  imageUrl?: string;
  layersJson?: string | null;
  isDefault?: boolean;
  isEnabled?: boolean;
  sortOrder?: number;
  description?: string | null;
}

export interface CertificateVerifyDto {
  isValid?: boolean;
  message?: string | null;
  certificate?: CertificateDto | null;
}

export interface CreateUpdateCertificateTemplateDto {
  name?: string;
  imageUrl?: string;
  layersJson?: string | null;
  isDefault?: boolean | null;
  isEnabled?: boolean | null;
  sortOrder?: number | null;
  description?: string | null;
}

export interface GetCertificateApplicationsInput extends PagedAndSortedResultRequestDto {
  status?: CertificateApplicationStatus | null;
  filter?: string | null;
}

export interface RejectCertificateInput {
  applicationId?: string;
  reviewRemark?: string | null;
}
