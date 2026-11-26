import type { CreatePracticeSessionInput, PracticeAttemptResultDto, PracticePaperDto, PracticeSessionDto, PracticeStatsDto, SubmitPracticeInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ClassroomPracticeService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticeSessionDto>({
      method: 'POST',
      url: `/api/app/classroom-practice/${id}/close`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePracticeSessionInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticeSessionDto>({
      method: 'POST',
      url: '/api/app/classroom-practice',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/classroom-practice/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticeSessionDto>({
      method: 'GET',
      url: `/api/app/classroom-practice/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (courseId: string, skipCount: number, maxResultCount: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PracticeSessionDto>>({
      method: 'GET',
      url: '/api/app/classroom-practice',
      params: { courseId, skipCount, maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMySessions = (courseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticeSessionDto[]>({
      method: 'GET',
      url: '/api/app/classroom-practice/my-sessions',
      params: { courseId },
    },
    { apiName: this.apiName,...config });
  

  getPaper = (token: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticePaperDto>({
      method: 'GET',
      url: '/api/app/classroom-practice/paper',
      params: { token },
    },
    { apiName: this.apiName,...config });
  

  getStats = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticeStatsDto>({
      method: 'GET',
      url: `/api/app/classroom-practice/${id}/stats`,
    },
    { apiName: this.apiName,...config });
  

  submit = (input: SubmitPracticeInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticeAttemptResultDto>({
      method: 'POST',
      url: '/api/app/classroom-practice/submit',
      body: input,
    },
    { apiName: this.apiName,...config });
}