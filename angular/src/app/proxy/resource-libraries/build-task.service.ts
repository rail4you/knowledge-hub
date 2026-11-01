import type { BuildTaskStatus } from './build-task-status.enum';
import type { BuildTaskDeliveryDto, BuildTaskDto, BuildTaskListRequestDto, BuildTaskTemplateDto, CreateBuildTaskDto, CreateBuildTaskTemplateDto, CreateLibraryTemplateDto, LibraryTemplateDto, ReviewBuildTaskDeliveryDto, SubmitBuildTaskDeliveryDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BuildTaskService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  closeTask = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BuildTaskDto>({
      method: 'POST',
      url: `/api/app/build-task/${id}/close-task`,
    },
    { apiName: this.apiName,...config });
  

  createTask = (input: CreateBuildTaskDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BuildTaskDto>({
      method: 'POST',
      url: '/api/app/build-task/task',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createTaskTemplate = (input: CreateBuildTaskTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BuildTaskTemplateDto>({
      method: 'POST',
      url: '/api/app/build-task/task-template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createTemplate = (input: CreateLibraryTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LibraryTemplateDto>({
      method: 'POST',
      url: '/api/app/build-task/template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getMyTasks = (status: BuildTaskStatus, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BuildTaskDto[]>({
      method: 'GET',
      url: '/api/app/build-task/my-tasks',
      params: { status },
    },
    { apiName: this.apiName,...config });
  

  getTask = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BuildTaskDto>({
      method: 'GET',
      url: `/api/app/build-task/${id}/task`,
    },
    { apiName: this.apiName,...config });
  

  getTaskTemplates = (libraryId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BuildTaskTemplateDto[]>({
      method: 'GET',
      url: `/api/app/build-task/task-templates/${libraryId}`,
    },
    { apiName: this.apiName,...config });
  

  getTasks = (input: BuildTaskListRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BuildTaskDto>>({
      method: 'GET',
      url: '/api/app/build-task/tasks',
      params: { libraryId: input.libraryId, status: input.status, onlyMine: input.onlyMine, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTemplates = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, LibraryTemplateDto[]>({
      method: 'GET',
      url: '/api/app/build-task/templates',
    },
    { apiName: this.apiName,...config });
  

  publishTask = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BuildTaskDto>({
      method: 'POST',
      url: `/api/app/build-task/${id}/publish-task`,
    },
    { apiName: this.apiName,...config });
  

  publishTemplate = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LibraryTemplateDto>({
      method: 'POST',
      url: `/api/app/build-task/${id}/publish-template`,
    },
    { apiName: this.apiName,...config });
  

  reviewDelivery = (input: ReviewBuildTaskDeliveryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BuildTaskDeliveryDto>({
      method: 'POST',
      url: '/api/app/build-task/review-delivery',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submitDelivery = (input: SubmitBuildTaskDeliveryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BuildTaskDeliveryDto>({
      method: 'POST',
      url: '/api/app/build-task/submit-delivery',
      body: input,
    },
    { apiName: this.apiName,...config });
}