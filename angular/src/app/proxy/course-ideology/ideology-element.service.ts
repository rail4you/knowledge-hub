import type { CreateUpdateIdeologyElementDto, IdeologyElementDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IdeologyElementService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateUpdateIdeologyElementDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyElementDto>({
      method: 'POST',
      url: '/api/app/ideology-element',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/ideology-element/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (onlyEnabled?: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyElementDto[]>({
      method: 'GET',
      url: '/api/app/ideology-element',
      params: { onlyEnabled },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateIdeologyElementDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyElementDto>({
      method: 'PUT',
      url: `/api/app/ideology-element/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}