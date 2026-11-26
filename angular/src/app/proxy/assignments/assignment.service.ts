import type { AssignmentDetailDto, AssignmentFileDto, AssignmentListItemDto, AssignmentListQueryDto, AssignmentStatisticsDto, AssignmentSubmissionDetailDto, AssignmentSubmissionItemDto, AssignmentSubmissionQueryDto, CreateUpdateAssignmentDto, GradeAssignmentSubmissionDto, StudentAssignmentDetailDto, StudentAssignmentFeedbackDto, StudentAssignmentItemDto, StudentSubmitAssignmentDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

@Injectable({
  providedIn: 'root',
})
export class AssignmentService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssignmentDetailDto>({
      method: 'POST',
      url: `/api/app/assignment/${id}/close`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateAssignmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssignmentDetailDto>({
      method: 'POST',
      url: '/api/app/assignment',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/assignment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssignmentDetailDto>({
      method: 'GET',
      url: `/api/app/assignment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getExportScores = (assignmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'GET',
      responseType: 'text',
      url: `/api/app/assignment/export-scores/${assignmentId}`,
    },
    { apiName: this.apiName,...config });
  

  getFile = (fileId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'GET',
      url: `/api/app/assignment/file/${fileId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: AssignmentListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssignmentListItemDto>>({
      method: 'GET',
      url: '/api/app/assignment',
      params: { courseId: input.courseId, status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyAssignment = (assignmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentAssignmentDetailDto>({
      method: 'GET',
      url: `/api/app/assignment/my-assignment/${assignmentId}`,
    },
    { apiName: this.apiName,...config });
  

  getMyAssignments = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentAssignmentItemDto[]>({
      method: 'GET',
      url: `/api/app/assignment/my-assignments/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getMyFeedback = (assignmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentAssignmentFeedbackDto>({
      method: 'GET',
      url: `/api/app/assignment/my-feedback/${assignmentId}`,
    },
    { apiName: this.apiName,...config });
  

  getStatistics = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssignmentStatisticsDto>({
      method: 'GET',
      url: `/api/app/assignment/statistics/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getSubmission = (submissionId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssignmentSubmissionDetailDto>({
      method: 'GET',
      url: `/api/app/assignment/submission/${submissionId}`,
    },
    { apiName: this.apiName,...config });
  

  getSubmissions = (input: AssignmentSubmissionQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssignmentSubmissionItemDto>>({
      method: 'GET',
      url: '/api/app/assignment/submissions',
      params: { assignmentId: input.assignmentId, status: input.status, onlyNotSubmitted: input.onlyNotSubmitted, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  grade = (submissionId: string, input: GradeAssignmentSubmissionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssignmentSubmissionDetailDto>({
      method: 'POST',
      url: `/api/app/assignment/grade/${submissionId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  publish = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssignmentDetailDto>({
      method: 'POST',
      url: `/api/app/assignment/${id}/publish`,
    },
    { apiName: this.apiName,...config });
  

  submit = (input: StudentSubmitAssignmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentAssignmentDetailDto>({
      method: 'POST',
      url: '/api/app/assignment/submit',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateAssignmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssignmentDetailDto>({
      method: 'PUT',
      url: `/api/app/assignment/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  uploadFile = (assignmentId: string, file: IFormFile, purpose?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssignmentFileDto>({
      method: 'POST',
      url: `/api/app/assignment/upload-file/${assignmentId}`,
      params: { purpose },
      body: file,
    },
    { apiName: this.apiName,...config });
}