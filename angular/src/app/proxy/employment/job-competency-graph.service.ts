import type { JobCompetencyGraphDetailDto, JobCompetencyGraphDto, JobCoreTaskDto, JobGraphCoverageDto, JobOptionDto, JobTaskCompetencyDto, LinkJobTaskCompetencyInput, SaveJobCompetencyGraphInput, SaveJobCoreTaskInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class JobCompetencyGraphService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: SaveJobCompetencyGraphInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCompetencyGraphDto>({
      method: 'POST',
      url: '/api/app/job-competency-graph',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createFromJob = (jobPostingId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCompetencyGraphDetailDto>({
      method: 'POST',
      url: `/api/app/job-competency-graph/from-job/${jobPostingId}`,
    },
    { apiName: this.apiName,...config });
  

  createTask = (input: SaveJobCoreTaskInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCoreTaskDto>({
      method: 'POST',
      url: '/api/app/job-competency-graph/task',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/job-competency-graph/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteTask = (taskId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/job-competency-graph/task/${taskId}`,
    },
    { apiName: this.apiName,...config });
  

  getCoverage = (id: string, courseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobGraphCoverageDto>({
      method: 'GET',
      url: `/api/app/job-competency-graph/${id}/coverage`,
      params: { courseId },
    },
    { apiName: this.apiName,...config });
  

  getDetail = (id: string, courseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCompetencyGraphDetailDto>({
      method: 'GET',
      url: `/api/app/job-competency-graph/${id}/detail`,
      params: { courseId },
    },
    { apiName: this.apiName,...config });
  

  getJobOptions = (filter?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobOptionDto[]>({
      method: 'GET',
      url: '/api/app/job-competency-graph/job-options',
      params: { filter },
    },
    { apiName: this.apiName,...config });
  

  getList = (majorId?: string, filter?: string, jobPostingId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCompetencyGraphDto[]>({
      method: 'GET',
      url: '/api/app/job-competency-graph',
      params: { majorId, filter, jobPostingId },
    },
    { apiName: this.apiName,...config });
  

  linkCompetency = (input: LinkJobTaskCompetencyInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobTaskCompetencyDto>({
      method: 'POST',
      url: '/api/app/job-competency-graph/link-competency',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  unlinkCompetency = (linkId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/job-competency-graph/unlink-competency/${linkId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: SaveJobCompetencyGraphInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCompetencyGraphDto>({
      method: 'PUT',
      url: `/api/app/job-competency-graph/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateTask = (taskId: string, input: SaveJobCoreTaskInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCoreTaskDto>({
      method: 'PUT',
      url: `/api/app/job-competency-graph/task/${taskId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}