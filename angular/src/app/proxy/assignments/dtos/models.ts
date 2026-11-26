import type { AssignmentType } from '../enums/assignment-type.enum';
import type { AssignmentStatus } from '../enums/assignment-status.enum';
import type { AssignmentFileType } from '../enums/assignment-file-type.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { SubmissionStatus } from '../enums/submission-status.enum';

export interface AssignmentCriterionDto {
  id?: string | null;
  name?: string;
  maxScore?: number;
  sortOrder?: number;
  description?: string | null;
}

export interface AssignmentDetailDto {
  id?: string;
  courseId?: string;
  courseTitle?: string;
  chapterId?: string | null;
  title?: string;
  description?: string | null;
  type?: AssignmentType;
  status?: AssignmentStatus;
  maxScore?: number;
  deadline?: string;
  allowLate?: boolean;
  latePenaltyPercent?: number;
  allowedFileTypes?: string;
  maxFileSizeMB?: number;
  maxFileCount?: number;
  allowResubmit?: boolean;
  teacherId?: string;
  teacherName?: string | null;
  creationTime?: string;
  publishTime?: string | null;
  closedTime?: string | null;
  criteria?: AssignmentCriterionDto[];
  attachments?: AssignmentFileDto[];
  totalStudents?: number;
  submittedCount?: number;
  gradedCount?: number;
  pendingGradeCount?: number;
  notSubmittedCount?: number;
  lateCount?: number;
  averageScore?: number;
}

export interface AssignmentFileDto {
  id?: string;
  fileName?: string;
  fileSize?: number;
  contentType?: string | null;
  type?: AssignmentFileType;
  version?: number;
  sortOrder?: number;
  creationTime?: string;
}

export interface AssignmentListItemDto {
  id?: string;
  courseId?: string;
  courseTitle?: string;
  title?: string;
  description?: string | null;
  type?: AssignmentType;
  status?: AssignmentStatus;
  maxScore?: number;
  deadline?: string;
  allowLate?: boolean;
  latePenaltyPercent?: number;
  totalStudents?: number;
  submittedCount?: number;
  gradedCount?: number;
  pendingGradeCount?: number;
  notSubmittedCount?: number;
  lateCount?: number;
  averageScore?: number;
  creationTime?: string;
  publishTime?: string | null;
}

export interface AssignmentListQueryDto extends PagedAndSortedResultRequestDto {
  courseId?: string | null;
  status?: AssignmentStatus | null;
  filter?: string | null;
}

export interface AssignmentStatisticsDto {
  totalCount?: number;
  ongoingCount?: number;
  pendingGradeCount?: number;
  averageScore?: number;
  lateCount?: number;
  totalStudents?: number;
}

export interface AssignmentSubmissionDetailDto {
  submissionId?: string;
  assignmentId?: string;
  assignmentTitle?: string;
  studentId?: string;
  studentName?: string;
  studentNumber?: string | null;
  status?: SubmissionStatus;
  maxScore?: number;
  score?: number | null;
  note?: string | null;
  honorPledge?: boolean;
  isLate?: boolean;
  submittedAt?: string | null;
  reviewedAt?: string | null;
  reviewerName?: string | null;
  comment?: string | null;
  allowResubmit?: boolean;
  version?: number;
  files?: AssignmentFileDto[];
  annotatedFiles?: AssignmentFileDto[];
  scores?: SubmissionScoreDto[];
  criteria?: AssignmentCriterionDto[];
}

export interface AssignmentSubmissionItemDto {
  submissionId?: string;
  assignmentId?: string;
  assignmentTitle?: string;
  studentId?: string;
  studentName?: string;
  studentNumber?: string | null;
  status?: SubmissionStatus;
  submitted?: boolean;
  isLate?: boolean;
  score?: number | null;
  maxScore?: number;
  submittedAt?: string | null;
  reviewedAt?: string | null;
  version?: number;
  fileCount?: number;
}

export interface AssignmentSubmissionQueryDto extends PagedAndSortedResultRequestDto {
  assignmentId?: string;
  status?: SubmissionStatus | null;
  onlyNotSubmitted?: boolean | null;
  filter?: string | null;
}

export interface CreateUpdateAssignmentDto {
  courseId?: string;
  chapterId?: string | null;
  title?: string;
  description?: string | null;
  type?: AssignmentType;
  maxScore?: number;
  deadline?: string;
  allowLate?: boolean;
  latePenaltyPercent?: number;
  allowedFileTypes?: string;
  maxFileSizeMB?: number;
  maxFileCount?: number;
  allowResubmit?: boolean;
  criteria?: AssignmentCriterionDto[];
  attachmentFileIds?: string[];
}

export interface GradeAssignmentSubmissionDto {
  score?: number | null;
  comment?: string | null;
  allowResubmit?: boolean;
  applyLatePenalty?: boolean;
  scores?: SubmissionScoreInputDto[];
}

export interface StudentAssignmentDetailDto {
  assignmentId?: string;
  courseId?: string;
  courseTitle?: string;
  title?: string;
  description?: string | null;
  type?: AssignmentType;
  maxScore?: number;
  deadline?: string;
  allowLate?: boolean;
  latePenaltyPercent?: number;
  allowedFileTypes?: string;
  maxFileSizeMB?: number;
  maxFileCount?: number;
  allowResubmit?: boolean;
  teacherName?: string | null;
  remainingDays?: number;
  criteria?: AssignmentCriterionDto[];
  attachments?: AssignmentFileDto[];
  submissionId?: string | null;
  myStatus?: SubmissionStatus;
  submitted?: boolean;
  isLate?: boolean;
  myNote?: string | null;
  honorPledge?: boolean;
  submittedAt?: string | null;
  version?: number;
  myFiles?: AssignmentFileDto[];
  canSubmit?: boolean;
}

export interface StudentAssignmentFeedbackDto {
  assignmentId?: string;
  title?: string;
  maxScore?: number;
  score?: number | null;
  classAverage?: number;
  graded?: boolean;
  comment?: string | null;
  reviewerName?: string | null;
  submittedAt?: string | null;
  reviewedAt?: string | null;
  isLate?: boolean;
  scores?: SubmissionScoreDto[];
  files?: AssignmentFileDto[];
  annotatedFiles?: AssignmentFileDto[];
  timeline?: StudentSubmissionTimelineDto[];
}

export interface StudentAssignmentItemDto {
  assignmentId?: string;
  title?: string;
  type?: AssignmentType;
  maxScore?: number;
  deadline?: string;
  status?: SubmissionStatus;
  submitted?: boolean;
  isLate?: boolean;
  score?: number | null;
  submittedAt?: string | null;
  hasFeedback?: boolean;
  remainingDays?: number;
}

export interface StudentSubmissionTimelineDto {
  label?: string;
  time?: string;
  extra?: string | null;
}

export interface StudentSubmitAssignmentDto {
  assignmentId?: string;
  note?: string | null;
  honorPledge?: boolean;
  isDraft?: boolean;
  fileIds?: string[];
}

export interface SubmissionScoreDto {
  id?: string;
  criterionId?: string | null;
  criterionName?: string;
  maxScore?: number;
  score?: number;
  comment?: string | null;
  sortOrder?: number;
}

export interface SubmissionScoreInputDto {
  criterionId?: string | null;
  criterionName?: string | null;
  score?: number | null;
  comment?: string | null;
  sortOrder?: number;
}
