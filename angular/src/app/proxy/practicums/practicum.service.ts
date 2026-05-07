import type { CreatePracticumAssessmentDto, CreatePracticumGuidanceRecordDto, CreatePracticumSubmissionDto, CreateUpdatePracticumProjectDto, GetPracticumEnrollmentsInput, GetPracticumSubmissionsInput, PagedPracticumProjectRequestDto, PracticumAssessmentDto, PracticumEnrollmentDto, PracticumGuidanceRecordDto, PracticumProjectDetailDto, PracticumProjectDto, PracticumSubmissionDto, PracticumTimelineItemDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PracticumService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  addGuidance = (input: CreatePracticumGuidanceRecordDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumGuidanceRecordDto>({
      method: 'POST',
      url: '/api/app/practicum/guidance',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdatePracticumProjectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumProjectDto>({
      method: 'POST',
      url: '/api/app/practicum',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createSubmission = (input: CreatePracticumSubmissionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumSubmissionDto>({
      method: 'POST',
      url: '/api/app/practicum/submission',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/practicum/${id}`,
    },
    { apiName: this.apiName,...config });
  

  enroll = (projectId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/practicum/enroll/${projectId}`,
    },
    { apiName: this.apiName,...config });
  

  exportAssessments = (projectId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/practicum/export-assessments/${projectId}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumProjectDto>({
      method: 'GET',
      url: `/api/app/practicum/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getDetail = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumProjectDetailDto>({
      method: 'GET',
      url: `/api/app/practicum/${id}/detail`,
    },
    { apiName: this.apiName,...config });
  

  getEnrollmentList = (input: GetPracticumEnrollmentsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PracticumEnrollmentDto>>({
      method: 'GET',
      url: '/api/app/practicum/enrollment-list',
      params: { projectId: input.projectId, studentId: input.studentId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getGuidanceList = (enrollmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumGuidanceRecordDto[]>({
      method: 'GET',
      url: `/api/app/practicum/guidance-list/${enrollmentId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedPracticumProjectRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PracticumProjectDto>>({
      method: 'GET',
      url: '/api/app/practicum',
      params: { filter: input.filter, courseId: input.courseId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyEnrollments = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumEnrollmentDto[]>({
      method: 'GET',
      url: '/api/app/practicum/my-enrollments',
    },
    { apiName: this.apiName,...config });
  

  getPublished = (input: PagedPracticumProjectRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PracticumProjectDto>>({
      method: 'GET',
      url: '/api/app/practicum/published',
      params: { filter: input.filter, courseId: input.courseId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getSubmissionList = (input: GetPracticumSubmissionsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PracticumSubmissionDto>>({
      method: 'GET',
      url: '/api/app/practicum/submission-list',
      params: { projectId: input.projectId, taskId: input.taskId, enrollmentId: input.enrollmentId, studentId: input.studentId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTimeline = (enrollmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumTimelineItemDto[]>({
      method: 'GET',
      url: `/api/app/practicum/timeline/${enrollmentId}`,
    },
    { apiName: this.apiName,...config });
  

  scoreEnrollment = (enrollmentId: string, input: CreatePracticumAssessmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumAssessmentDto>({
      method: 'POST',
      url: `/api/app/practicum/score-enrollment/${enrollmentId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdatePracticumProjectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumProjectDto>({
      method: 'PUT',
      url: `/api/app/practicum/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}