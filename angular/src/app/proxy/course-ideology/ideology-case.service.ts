import type { CreateUpdateIdeologyCaseDto, IdeologyCaseDto, IdeologyCaseQueryDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IdeologyCaseService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  archive = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyCaseDto>({
      method: 'POST',
      url: `/api/app/ideology-case/${id}/archive`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateIdeologyCaseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyCaseDto>({
      method: 'POST',
      url: '/api/app/ideology-case',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/ideology-case/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyCaseDto>({
      method: 'GET',
      url: `/api/app/ideology-case/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: IdeologyCaseQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IdeologyCaseDto>>({
      method: 'GET',
      url: '/api/app/ideology-case',
      params: { courseId: input.courseId, chapterId: input.chapterId, elementId: input.elementId, status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  publish = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyCaseDto>({
      method: 'POST',
      url: `/api/app/ideology-case/${id}/publish`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateIdeologyCaseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyCaseDto>({
      method: 'PUT',
      url: `/api/app/ideology-case/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}