import type { AiGenerationTaskDto, CreateAiGenerationTaskDto, GetAiGenerationTaskListDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AiGenerationTaskService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/ai-generation-task/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateAiGenerationTaskDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AiGenerationTaskDto>({
      method: 'POST',
      url: '/api/app/ai-generation-task',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/ai-generation-task/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AiGenerationTaskDto>({
      method: 'GET',
      url: `/api/app/ai-generation-task/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAiGenerationTaskListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AiGenerationTaskDto>>({
      method: 'GET',
      url: '/api/app/ai-generation-task',
      params: { taskType: input.taskType, status: input.status, filter: input.filter, startTime: input.startTime, endTime: input.endTime, onlyMine: input.onlyMine, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyCompleted = (unreadOnly?: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AiGenerationTaskDto[]>({
      method: 'GET',
      url: '/api/app/ai-generation-task/my-completed',
      params: { unreadOnly },
    },
    { apiName: this.apiName,...config });
  

  getMyUnreadCount = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: '/api/app/ai-generation-task/my-unread-count',
    },
    { apiName: this.apiName,...config });
  

  getResult = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'GET',
      responseType: 'text',
      url: `/api/app/ai-generation-task/${id}/result`,
    },
    { apiName: this.apiName,...config });
  

  markAllAsRead = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/ai-generation-task/mark-all-as-read',
    },
    { apiName: this.apiName,...config });
  

  markAsRead = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/ai-generation-task/${id}/mark-as-read`,
    },
    { apiName: this.apiName,...config });
  

  retry = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/ai-generation-task/${id}/retry`,
    },
    { apiName: this.apiName,...config });
}