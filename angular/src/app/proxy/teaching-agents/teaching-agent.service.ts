import type { CloneTeachingAgentFromPresetDto, CreateUpdateTeachingAgentDto, PagedTeachingAgentRequestDto, PublishTeachingAgentVersionDto, TeachingAgentDetailDto, TeachingAgentDto, TeachingAgentPresetDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TeachingAgentService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  cloneFromPreset = (input: CloneTeachingAgentFromPresetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingAgentDto>({
      method: 'POST',
      url: '/api/app/teaching-agent/clone-from-preset',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createDraft = (input: CreateUpdateTeachingAgentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingAgentDto>({
      method: 'POST',
      url: '/api/app/teaching-agent/draft',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getDetail = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingAgentDetailDto>({
      method: 'GET',
      url: `/api/app/teaching-agent/${id}/detail`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedTeachingAgentRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TeachingAgentDto>>({
      method: 'GET',
      url: '/api/app/teaching-agent',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPresets = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingAgentPresetDto[]>({
      method: 'GET',
      url: '/api/app/teaching-agent/presets',
    },
    { apiName: this.apiName,...config });
  

  publishVersion = (id: string, input: PublishTeachingAgentVersionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingAgentDto>({
      method: 'POST',
      url: `/api/app/teaching-agent/${id}/publish-version`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateDraft = (id: string, input: CreateUpdateTeachingAgentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingAgentDto>({
      method: 'PUT',
      url: `/api/app/teaching-agent/${id}/draft`,
      body: input,
    },
    { apiName: this.apiName,...config });
}