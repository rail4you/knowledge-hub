import { Injectable, inject } from '@angular/core';
import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';

/** 与后端 KnowledgeHub.Assignments.Enums.AssignmentType 对齐 */
export enum AssignmentType {
  Individual = 0,
  Group = 1,
}

/** 与后端 KnowledgeHub.Assignments.Enums.AssignmentStatus 对齐 */
export enum AssignmentStatus {
  Draft = 0,
  Published = 1,
  Closed = 2,
}

/** 与后端 KnowledgeHub.Assignments.Enums.SubmissionStatus 对齐 */
export enum SubmissionStatus {
  Draft = 0,
  Submitted = 1,
  Graded = 2,
}

/** 与后端 KnowledgeHub.Assignments.Enums.AssignmentFileType 对齐 */
export enum AssignmentFileType {
  Requirement = 0,
  Submission = 1,
  Annotated = 2,
}

export interface AssignmentFileItem {
  id: string;
  fileName: string;
  fileSize: number;
  contentType?: string | null;
  type: AssignmentFileType;
  version: number;
  sortOrder: number;
  creationTime: string;
}

export interface AssignmentCriterionItem {
  id?: string | null;
  name: string;
  maxScore: number;
  sortOrder: number;
  description?: string | null;
}

export interface AssignmentListItem {
  id: string;
  courseId: string;
  courseTitle: string;
  title: string;
  description?: string | null;
  type: AssignmentType;
  status: AssignmentStatus;
  maxScore: number;
  deadline: string;
  allowLate: boolean;
  latePenaltyPercent: number;
  totalStudents: number;
  submittedCount: number;
  gradedCount: number;
  pendingGradeCount: number;
  notSubmittedCount: number;
  lateCount: number;
  averageScore: number;
  creationTime: string;
  publishTime?: string | null;
}

export interface AssignmentStatistics {
  totalCount: number;
  ongoingCount: number;
  pendingGradeCount: number;
  averageScore: number;
  lateCount: number;
  totalStudents: number;
}

export interface AssignmentDetail {
  id: string;
  courseId: string;
  courseTitle: string;
  chapterId?: string | null;
  title: string;
  description?: string | null;
  type: AssignmentType;
  status: AssignmentStatus;
  maxScore: number;
  deadline: string;
  allowLate: boolean;
  latePenaltyPercent: number;
  allowedFileTypes: string;
  maxFileSizeMB: number;
  maxFileCount: number;
  allowResubmit: boolean;
  teacherId: string;
  teacherName?: string | null;
  creationTime: string;
  publishTime?: string | null;
  closedTime?: string | null;
  criteria: AssignmentCriterionItem[];
  attachments: AssignmentFileItem[];
  totalStudents: number;
  submittedCount: number;
  gradedCount: number;
  pendingGradeCount: number;
  notSubmittedCount: number;
  lateCount: number;
  averageScore: number;
}

export interface CreateUpdateAssignmentInput {
  courseId: string;
  chapterId?: string | null;
  title: string;
  description?: string | null;
  type: AssignmentType;
  maxScore: number;
  deadline: string;
  allowLate: boolean;
  latePenaltyPercent: number;
  allowedFileTypes: string;
  maxFileSizeMB: number;
  maxFileCount: number;
  allowResubmit: boolean;
  criteria: AssignmentCriterionItem[];
  attachmentFileIds: string[];
}

export interface AssignmentSubmissionItem {
  submissionId: string;
  assignmentId: string;
  assignmentTitle: string;
  studentId: string;
  studentName: string;
  studentNumber?: string | null;
  status: SubmissionStatus;
  submitted: boolean;
  isLate: boolean;
  score?: number | null;
  maxScore: number;
  submittedAt?: string | null;
  reviewedAt?: string | null;
  version: number;
  fileCount: number;
}

export interface SubmissionScoreItem {
  id: string;
  criterionId?: string | null;
  criterionName: string;
  maxScore: number;
  score: number;
  comment?: string | null;
  sortOrder: number;
}

export interface AssignmentSubmissionDetail {
  submissionId: string;
  assignmentId: string;
  assignmentTitle: string;
  studentId: string;
  studentName: string;
  studentNumber?: string | null;
  status: SubmissionStatus;
  maxScore: number;
  score?: number | null;
  note?: string | null;
  honorPledge: boolean;
  isLate: boolean;
  submittedAt?: string | null;
  reviewedAt?: string | null;
  reviewerName?: string | null;
  comment?: string | null;
  allowResubmit: boolean;
  version: number;
  files: AssignmentFileItem[];
  annotatedFiles: AssignmentFileItem[];
  scores: SubmissionScoreItem[];
  criteria: AssignmentCriterionItem[];
}

export interface StudentAssignmentItem {
  assignmentId: string;
  title: string;
  type: AssignmentType;
  maxScore: number;
  deadline: string;
  status: SubmissionStatus;
  submitted: boolean;
  isLate: boolean;
  score?: number | null;
  submittedAt?: string | null;
  hasFeedback: boolean;
  remainingDays: number;
}

export interface StudentAssignmentDetail {
  assignmentId: string;
  courseId: string;
  courseTitle: string;
  title: string;
  description?: string | null;
  type: AssignmentType;
  maxScore: number;
  deadline: string;
  allowLate: boolean;
  latePenaltyPercent: number;
  allowedFileTypes: string;
  maxFileSizeMB: number;
  maxFileCount: number;
  allowResubmit: boolean;
  teacherName?: string | null;
  remainingDays: number;
  criteria: AssignmentCriterionItem[];
  attachments: AssignmentFileItem[];
  submissionId?: string | null;
  myStatus: SubmissionStatus;
  submitted: boolean;
  isLate: boolean;
  myNote?: string | null;
  honorPledge: boolean;
  submittedAt?: string | null;
  version: number;
  myFiles: AssignmentFileItem[];
  canSubmit: boolean;
}

export interface StudentSubmissionTimeline {
  label: string;
  time: string;
  extra?: string | null;
}

export interface StudentAssignmentFeedback {
  assignmentId: string;
  title: string;
  maxScore: number;
  score?: number | null;
  classAverage: number;
  graded: boolean;
  comment?: string | null;
  reviewerName?: string | null;
  submittedAt?: string | null;
  reviewedAt?: string | null;
  isLate: boolean;
  scores: SubmissionScoreItem[];
  files: AssignmentFileItem[];
  annotatedFiles: AssignmentFileItem[];
  timeline: StudentSubmissionTimeline[];
}

@Injectable({ providedIn: 'root' })
export class AssignmentApiService {
  private readonly rest = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  private cfg(): Partial<Rest.Config> {
    return { apiName: this.apiName } as Partial<Rest.Config>;
  }

  // ── 教师端 ──────────────────────────────────────────────
  getList = (input: {
    courseId?: string;
    status?: AssignmentStatus;
    filter?: string;
    skipCount?: number;
    maxResultCount?: number;
  }) =>
    this.rest.request<any, PagedResultDto<AssignmentListItem>>(
      {
        method: 'GET',
        url: '/api/app/assignment',
        params: {
          courseId: input.courseId,
          status: input.status,
          filter: input.filter,
          skipCount: input.skipCount ?? 0,
          maxResultCount: input.maxResultCount ?? 10,
        },
      },
      this.cfg(),
    );

  getStatistics = (courseId: string) =>
    this.rest.request<any, AssignmentStatistics>(
      { method: 'GET', url: `/api/app/assignment/statistics/${courseId}` },
      this.cfg(),
    );

  get = (id: string) =>
    this.rest.request<any, AssignmentDetail>(
      { method: 'GET', url: `/api/app/assignment/${id}` },
      this.cfg(),
    );

  create = (input: CreateUpdateAssignmentInput) =>
    this.rest.request<any, AssignmentDetail>(
      { method: 'POST', url: '/api/app/assignment', body: input },
      this.cfg(),
    );

  update = (id: string, input: CreateUpdateAssignmentInput) =>
    this.rest.request<any, AssignmentDetail>(
      { method: 'PUT', url: `/api/app/assignment/${id}`, body: input },
      this.cfg(),
    );

  publish = (id: string) =>
    this.rest.request<any, AssignmentDetail>(
      { method: 'POST', url: `/api/app/assignment/${id}/publish` },
      this.cfg(),
    );

  close = (id: string) =>
    this.rest.request<any, AssignmentDetail>(
      { method: 'POST', url: `/api/app/assignment/${id}/close` },
      this.cfg(),
    );

  delete = (id: string) =>
    this.rest.request<any, void>(
      { method: 'DELETE', url: `/api/app/assignment/${id}` },
      this.cfg(),
    );

  getExportScores = (assignmentId: string) =>
    this.rest.request<any, string>(
      { method: 'GET', url: `/api/app/assignment/export-scores/${assignmentId}` },
      this.cfg(),
    );

  getSubmissions = (input: {
    assignmentId: string;
    status?: SubmissionStatus;
    onlyNotSubmitted?: boolean;
    filter?: string;
    skipCount?: number;
    maxResultCount?: number;
  }) =>
    this.rest.request<any, PagedResultDto<AssignmentSubmissionItem>>(
      {
        method: 'GET',
        url: '/api/app/assignment/submissions',
        params: {
          assignmentId: input.assignmentId,
          status: input.status,
          onlyNotSubmitted: input.onlyNotSubmitted,
          filter: input.filter,
          skipCount: input.skipCount ?? 0,
          maxResultCount: input.maxResultCount ?? 50,
        },
      },
      this.cfg(),
    );

  getSubmission = (submissionId: string) =>
    this.rest.request<any, AssignmentSubmissionDetail>(
      { method: 'GET', url: `/api/app/assignment/submission/${submissionId}` },
      this.cfg(),
    );

  grade = (
    submissionId: string,
    input: {
      score?: number | null;
      comment?: string | null;
      allowResubmit?: boolean;
      applyLatePenalty?: boolean;
      scores: {
        criterionId?: string | null;
        criterionName?: string | null;
        score?: number | null;
        comment?: string | null;
        sortOrder?: number;
      }[];
    },
  ) =>
    this.rest.request<any, AssignmentSubmissionDetail>(
      { method: 'POST', url: `/api/app/assignment/grade/${submissionId}`, body: input },
      this.cfg(),
    );

  /** 上传文件（作业要求附件 / 学生提交件 / 教师批注版） */
  uploadFile = (assignmentId: string, file: File, purpose: 'requirement' | 'submission' | 'annotated') => {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.rest.request<any, AssignmentFileItem>(
      {
        method: 'POST',
        url: `/api/app/assignment/upload-file/${assignmentId}`,
        params: { purpose },
        body: formData as any,
      },
      this.cfg(),
    );
  };

  /** 下载文件（返回 base64） */
  getFile = (fileId: string) =>
    this.rest.request<any, string>(
      { method: 'GET', url: `/api/app/assignment/file/${fileId}` },
      this.cfg(),
    );

  // ── 学生端 ──────────────────────────────────────────────
  getMyAssignments = (courseId: string) =>
    this.rest.request<any, StudentAssignmentItem[]>(
      { method: 'GET', url: `/api/app/assignment/my-assignments/${courseId}` },
      this.cfg(),
    );

  getMyAssignment = (assignmentId: string) =>
    this.rest.request<any, StudentAssignmentDetail>(
      { method: 'GET', url: `/api/app/assignment/my-assignment/${assignmentId}` },
      this.cfg(),
    );

  submit = (input: {
    assignmentId: string;
    note?: string | null;
    honorPledge: boolean;
    isDraft: boolean;
    fileIds: string[];
  }) =>
    this.rest.request<any, StudentAssignmentDetail>(
      { method: 'POST', url: '/api/app/assignment/submit', body: input },
      this.cfg(),
    );

  getMyFeedback = (assignmentId: string) =>
    this.rest.request<any, StudentAssignmentFeedback>(
      { method: 'GET', url: `/api/app/assignment/my-feedback/${assignmentId}` },
      this.cfg(),
    );
}

export const ASSIGNMENT_TYPE_LABELS: Record<number, string> = {
  [AssignmentType.Individual]: '个人',
  [AssignmentType.Group]: '小组',
};

export const ASSIGNMENT_STATUS_LABELS: Record<number, string> = {
  [AssignmentStatus.Draft]: '草稿 · 未发布',
  [AssignmentStatus.Published]: '进行中',
  [AssignmentStatus.Closed]: '已结束',
};

export const SUBMISSION_STATUS_LABELS: Record<number, string> = {
  [SubmissionStatus.Draft]: '待提交',
  [SubmissionStatus.Submitted]: '待批阅',
  [SubmissionStatus.Graded]: '已批阅',
};

/** 文件体积格式化 */
export function formatFileSize(bytes: number): string {
  if (!bytes) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let index = 0;
  while (value >= 1024 && index < units.length - 1) {
    value = value / 1024;
    index++;
  }
  return `${value.toFixed(index === 0 ? 0 : 1)} ${units[index]}`;
}
