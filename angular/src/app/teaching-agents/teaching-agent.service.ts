import { Rest, RestService } from '@abp/ng.core';
import { inject, Injectable } from '@angular/core';
import {
  CreateUpdateTeachingAgentPayload,
  PagedResultDto,
  TeachingAgent,
  TeachingAgentDetail,
  TeachingAgentPreset,
} from './models';

@Injectable({ providedIn: 'root' })
export class TeachingAgentService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getPresets(config?: Partial<Rest.Config>) {
    return this.restService.request<any, TeachingAgentPreset[]>({
      method: 'GET',
      url: '/api/teaching-agents/presets',
    }, { apiName: this.apiName, ...config });
  }

  getList(input: { filter?: string; skipCount?: number; maxResultCount?: number; sorting?: string }, config?: Partial<Rest.Config>) {
    return this.restService.request<any, PagedResultDto<TeachingAgent>>({
      method: 'GET',
      url: '/api/teaching-agents',
      params: input,
    }, { apiName: this.apiName, ...config });
  }

  get(id: string, config?: Partial<Rest.Config>) {
    return this.restService.request<any, TeachingAgentDetail>({
      method: 'GET',
      url: `/api/teaching-agents/${id}`,
    }, { apiName: this.apiName, ...config });
  }

  create(input: CreateUpdateTeachingAgentPayload, config?: Partial<Rest.Config>) {
    return this.restService.request<any, TeachingAgent>({
      method: 'POST',
      url: '/api/teaching-agents',
      body: input,
    }, { apiName: this.apiName, ...config });
  }

  update(id: string, input: CreateUpdateTeachingAgentPayload, config?: Partial<Rest.Config>) {
    return this.restService.request<any, TeachingAgent>({
      method: 'PUT',
      url: `/api/teaching-agents/${id}`,
      body: input,
    }, { apiName: this.apiName, ...config });
  }

  publish(id: string, versionNote?: string, config?: Partial<Rest.Config>) {
    return this.restService.request<any, TeachingAgent>({
      method: 'POST',
      url: `/api/teaching-agents/${id}/publish`,
      body: { versionNote: versionNote ?? null },
    }, { apiName: this.apiName, ...config });
  }

  cloneFromPreset(input: { presetCode: string; name: string; visibility: number }, config?: Partial<Rest.Config>) {
    return this.restService.request<any, TeachingAgent>({
      method: 'POST',
      url: '/api/teaching-agents/from-preset',
      body: input,
    }, { apiName: this.apiName, ...config });
  }
}
