import { Injectable, inject } from '@angular/core';
import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Observable } from 'rxjs';

export enum PracticumProjectStatus {
  Draft = 0,
  Published = 1,
  Archived = 2,
}

export enum PracticumEnrollmentStatus {
  Enrolled = 0,
  InProgress = 1,
  Submitted = 2,
  Reviewed = 3,
  Completed = 4,
  Cancelled = 5,
}

export enum PracticumSubmissionStatus {
  Submitted = 0,
  Returned = 1,
  Reviewed = 2,
}

export enum PracticumMaterialType {
  Guide = 0,
  Case = 1,
  Template = 2,
  Link = 3,
}

export interface PracticumTaskDto {
  id: string;
  projectId: string;
  title: string;
  description?: string;
  requirement?: string;
  dueTime?: string;
  scoreWeight: number;
  sortOrder: number;
}

export interface PracticumMaterialDto {
  id: string;
  projectId: string;
  taskId?: string;
  title: string;
  description?: string;
  materialType: PracticumMaterialType;
  resourceUrl: string;
  sortOrder: number;
}

export interface PracticumProjectDto {
  id: string;
  title: string;
  summary?: string;
  description?: string;
  coverImageUrl?: string;
  courseId?: string;
  courseTitle?: string;
  major?: string;
  className?: string;
  status: PracticumProjectStatus;
  startTime?: string;
  endTime?: string;
  maxScore: number;
  allowResubmission: boolean;
  taskCount: number;
  materialCount: number;
  enrollmentCount: number;
  isCurrentUserEnrolled: boolean;
  currentUserProgress?: number;
  creationTime: string;
}

export interface PracticumProjectDetailDto extends PracticumProjectDto {
  tasks: PracticumTaskDto[];
  materials: PracticumMaterialDto[];
}

export interface CreateUpdatePracticumTaskDto {
  title: string;
  description?: string;
  requirement?: string;
  dueTime?: string;
  scoreWeight: number;
  sortOrder: number;
}

export interface CreateUpdatePracticumMaterialDto {
  taskId?: string;
  title: string;
  description?: string;
  materialType: PracticumMaterialType;
  resourceUrl: string;
  sortOrder: number;
}

export interface CreateUpdatePracticumProjectDto {
  title: string;
  summary?: string;
  description?: string;
  coverImageUrl?: string;
  courseId?: string;
  major?: string;
  className?: string;
  status: PracticumProjectStatus;
  startTime?: string;
  endTime?: string;
  maxScore: number;
  allowResubmission: boolean;
  tasks: CreateUpdatePracticumTaskDto[];
  materials: CreateUpdatePracticumMaterialDto[];
}

export interface PagedPracticumProjectRequestDto {
  filter?: string;
  courseId?: string;
  status?: PracticumProjectStatus;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface PracticumEnrollmentDto {
  id: string;
  projectId: string;
  projectTitle?: string;
  studentId: string;
  studentName?: string;
  status: PracticumEnrollmentStatus;
  progress: number;
  enrolledAt: string;
  lastSubmittedAt?: string;
  finalScore?: number;
  finalComment?: string;
  completedAt?: string;
}

export interface GetPracticumEnrollmentsInput {
  projectId?: string;
  studentId?: string;
  status?: PracticumEnrollmentStatus;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface PracticumSubmissionDto {
  id: string;
  projectId: string;
  projectTitle?: string;
  taskId: string;
  taskTitle?: string;
  enrollmentId: string;
  studentId: string;
  studentName?: string;
  versionNo: number;
  content?: string;
  attachmentUrls?: string;
  linkUrl?: string;
  screenshotUrls?: string;
  status: PracticumSubmissionStatus;
  submittedAt: string;
  teacherFeedback?: string;
  reviewedAt?: string;
  score?: number;
}

export interface CreatePracticumSubmissionDto {
  projectId: string;
  taskId: string;
  content?: string;
  attachmentUrls?: string;
  linkUrl?: string;
  screenshotUrls?: string;
}

export interface GetPracticumSubmissionsInput {
  projectId?: string;
  taskId?: string;
  enrollmentId?: string;
  studentId?: string;
  status?: PracticumSubmissionStatus;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface PracticumGuidanceRecordDto {
  id: string;
  projectId: string;
  enrollmentId: string;
  taskId?: string;
  taskTitle?: string;
  teacherId: string;
  teacherName?: string;
  content: string;
  isVisibleToStudent: boolean;
  guidedAt: string;
}

export interface CreatePracticumGuidanceRecordDto {
  enrollmentId: string;
  taskId?: string;
  content: string;
  isVisibleToStudent: boolean;
}

export interface PracticumAssessmentDto {
  id: string;
  projectId: string;
  enrollmentId: string;
  submissionId?: string;
  teacherId: string;
  teacherName?: string;
  score: number;
  gradeLevel?: string;
  comment?: string;
  rubricJson?: string;
  assessedAt: string;
}

export interface CreatePracticumAssessmentDto {
  submissionId?: string;
  score: number;
  gradeLevel?: string;
  comment?: string;
  rubricJson?: string;
}

export interface PracticumTimelineItemDto {
  type: string;
  title: string;
  content?: string;
  operatorName?: string;
  time: string;
  metadata: Record<string, string | null>;
}

@Injectable({
  providedIn: 'root',
})
export class PracticumService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  get = (id: string) =>
    this.restService.request<any, PracticumProjectDto>({
      method: 'GET',
      url: `/api/app/practicum/${id}`,
    }, { apiName: this.apiName });

  getDetail = (id: string) =>
    this.restService.request<any, PracticumProjectDetailDto>({
      method: 'GET',
      url: `/api/app/practicum/${id}/detail`,
    }, { apiName: this.apiName });

  getList = (input: PagedPracticumProjectRequestDto) =>
    this.restService.request<any, PagedResultDto<PracticumProjectDto>>({
      method: 'GET',
      url: '/api/app/practicum',
      params: input,
    }, { apiName: this.apiName });

  getPublished = (input: PagedPracticumProjectRequestDto) =>
    this.restService.request<any, PagedResultDto<PracticumProjectDto>>({
      method: 'GET',
      url: '/api/app/practicum/published',
      params: input,
    }, { apiName: this.apiName });

  create = (input: CreateUpdatePracticumProjectDto) =>
    this.restService.request<any, PracticumProjectDto>({
      method: 'POST',
      url: '/api/app/practicum',
      body: input,
    }, { apiName: this.apiName });

  update = (id: string, input: CreateUpdatePracticumProjectDto) =>
    this.restService.request<any, PracticumProjectDto>({
      method: 'PUT',
      url: `/api/app/practicum/${id}`,
      body: input,
    }, { apiName: this.apiName });

  delete = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/practicum/${id}`,
    }, { apiName: this.apiName });

  enroll = (projectId: string) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/practicum/enroll/${projectId}`,
    }, { apiName: this.apiName });

  getMyEnrollments = () =>
    this.restService.request<any, PracticumEnrollmentDto[]>({
      method: 'GET',
      url: '/api/app/practicum/my-enrollments',
    }, { apiName: this.apiName });

  getEnrollmentList = (input: GetPracticumEnrollmentsInput) =>
    this.restService.request<any, PagedResultDto<PracticumEnrollmentDto>>({
      method: 'GET',
      url: '/api/app/practicum/enrollment-list',
      params: input,
    }, { apiName: this.apiName });

  createSubmission = (input: CreatePracticumSubmissionDto) =>
    this.restService.request<any, PracticumSubmissionDto>({
      method: 'POST',
      url: '/api/app/practicum/submission',
      body: input,
    }, { apiName: this.apiName });

  getSubmissionList = (input: GetPracticumSubmissionsInput) =>
    this.restService.request<any, PagedResultDto<PracticumSubmissionDto>>({
      method: 'GET',
      url: '/api/app/practicum/submission-list',
      params: input,
    }, { apiName: this.apiName });

  addGuidance = (input: CreatePracticumGuidanceRecordDto) =>
    this.restService.request<any, PracticumGuidanceRecordDto>({
      method: 'POST',
      url: '/api/app/practicum/guidance',
      body: input,
    }, { apiName: this.apiName });

  getGuidanceList = (enrollmentId: string) =>
    this.restService.request<any, PracticumGuidanceRecordDto[]>({
      method: 'GET',
      url: `/api/app/practicum/guidance-list/${enrollmentId}`,
    }, { apiName: this.apiName });

  scoreEnrollment = (enrollmentId: string, input: CreatePracticumAssessmentDto) =>
    this.restService.request<any, PracticumAssessmentDto>({
      method: 'POST',
      url: `/api/app/practicum/score-enrollment/${enrollmentId}`,
      body: input,
    }, { apiName: this.apiName });

  getTimeline = (enrollmentId: string) =>
    this.restService.request<any, PracticumTimelineItemDto[]>({
      method: 'GET',
      url: `/api/app/practicum/timeline/${enrollmentId}`,
    }, { apiName: this.apiName });

  exportAssessments = (projectId?: string, config?: Partial<Rest.Config>): Observable<Blob> =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: '/api/app/practicum/export-assessments',
      params: { projectId },
    }, { apiName: this.apiName, ...config });
}
