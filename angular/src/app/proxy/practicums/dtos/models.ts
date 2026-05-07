import type { PracticumMaterialType } from '../enums/practicum-material-type.enum';
import type { PracticumProjectStatus } from '../enums/practicum-project-status.enum';
import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { PracticumEnrollmentStatus } from '../enums/practicum-enrollment-status.enum';
import type { PracticumSubmissionStatus } from '../enums/practicum-submission-status.enum';

export interface CreatePracticumAssessmentDto {
  submissionId?: string | null;
  score?: number;
  gradeLevel?: string | null;
  comment?: string | null;
  rubricJson?: string | null;
}

export interface CreatePracticumGuidanceRecordDto {
  enrollmentId?: string;
  taskId?: string | null;
  content?: string;
  isVisibleToStudent?: boolean;
}

export interface CreatePracticumSubmissionDto {
  projectId?: string;
  taskId?: string;
  content?: string | null;
  attachmentUrls?: string | null;
  linkUrl?: string | null;
  screenshotUrls?: string | null;
}

export interface CreateUpdatePracticumMaterialDto {
  taskId?: string | null;
  title?: string;
  description?: string | null;
  materialType?: PracticumMaterialType;
  resourceUrl?: string;
  sortOrder?: number;
}

export interface CreateUpdatePracticumProjectDto {
  title?: string;
  summary?: string | null;
  description?: string | null;
  coverImageUrl?: string | null;
  courseId?: string | null;
  major?: string | null;
  className?: string | null;
  status?: PracticumProjectStatus;
  startTime?: string | null;
  endTime?: string | null;
  maxScore?: number;
  allowResubmission?: boolean;
  tasks?: CreateUpdatePracticumTaskDto[];
  materials?: CreateUpdatePracticumMaterialDto[];
}

export interface CreateUpdatePracticumTaskDto {
  title?: string;
  description?: string | null;
  requirement?: string | null;
  dueTime?: string | null;
  scoreWeight?: number;
  sortOrder?: number;
}

export interface GetPracticumEnrollmentsInput extends PagedAndSortedResultRequestDto {
  projectId?: string | null;
  studentId?: string | null;
  status?: PracticumEnrollmentStatus | null;
}

export interface GetPracticumSubmissionsInput extends PagedAndSortedResultRequestDto {
  projectId?: string | null;
  taskId?: string | null;
  enrollmentId?: string | null;
  studentId?: string | null;
  status?: PracticumSubmissionStatus | null;
}

export interface PagedPracticumProjectRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  courseId?: string | null;
  status?: PracticumProjectStatus | null;
}

export interface PracticumAssessmentDto extends FullAuditedEntityDto<string> {
  projectId?: string;
  enrollmentId?: string;
  submissionId?: string | null;
  teacherId?: string;
  teacherName?: string | null;
  score?: number;
  gradeLevel?: string | null;
  comment?: string | null;
  rubricJson?: string | null;
  assessedAt?: string;
}

export interface PracticumEnrollmentDto extends FullAuditedEntityDto<string> {
  projectId?: string;
  projectTitle?: string | null;
  studentId?: string;
  studentName?: string | null;
  status?: PracticumEnrollmentStatus;
  progress?: number;
  enrolledAt?: string;
  lastSubmittedAt?: string | null;
  finalScore?: number | null;
  finalComment?: string | null;
  completedAt?: string | null;
}

export interface PracticumGuidanceRecordDto extends FullAuditedEntityDto<string> {
  projectId?: string;
  enrollmentId?: string;
  taskId?: string | null;
  taskTitle?: string | null;
  teacherId?: string;
  teacherName?: string | null;
  content?: string;
  isVisibleToStudent?: boolean;
  guidedAt?: string;
}

export interface PracticumMaterialDto extends EntityDto<string> {
  projectId?: string;
  taskId?: string | null;
  title?: string;
  description?: string | null;
  materialType?: PracticumMaterialType;
  resourceUrl?: string;
  sortOrder?: number;
}

export interface PracticumProjectDetailDto extends PracticumProjectDto {
  tasks?: PracticumTaskDto[];
  materials?: PracticumMaterialDto[];
}

export interface PracticumProjectDto extends FullAuditedEntityDto<string> {
  title?: string;
  summary?: string | null;
  description?: string | null;
  coverImageUrl?: string | null;
  courseId?: string | null;
  courseTitle?: string | null;
  major?: string | null;
  className?: string | null;
  status?: PracticumProjectStatus;
  startTime?: string | null;
  endTime?: string | null;
  maxScore?: number;
  allowResubmission?: boolean;
  taskCount?: number;
  materialCount?: number;
  enrollmentCount?: number;
  isCurrentUserEnrolled?: boolean;
  currentUserProgress?: number | null;
}

export interface PracticumSubmissionDto extends FullAuditedEntityDto<string> {
  projectId?: string;
  projectTitle?: string | null;
  taskId?: string;
  taskTitle?: string | null;
  enrollmentId?: string;
  studentId?: string;
  studentName?: string | null;
  versionNo?: number;
  content?: string | null;
  attachmentUrls?: string | null;
  linkUrl?: string | null;
  screenshotUrls?: string | null;
  status?: PracticumSubmissionStatus;
  submittedAt?: string;
  teacherFeedback?: string | null;
  reviewedAt?: string | null;
  score?: number | null;
}

export interface PracticumTaskDto extends EntityDto<string> {
  projectId?: string;
  title?: string;
  description?: string | null;
  requirement?: string | null;
  dueTime?: string | null;
  scoreWeight?: number;
  sortOrder?: number;
}

export interface PracticumTimelineItemDto {
  type?: string;
  title?: string;
  content?: string | null;
  operatorName?: string | null;
  time?: string;
  metadata?: Record<string, string>;
}
