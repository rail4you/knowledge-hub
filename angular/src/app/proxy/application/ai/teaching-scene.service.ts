import type { CreateUpdateTeachingSceneDto, TeachingSceneDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { TeachingSceneCategory } from '../../ai/teaching-scene-category.enum';

@Injectable({
  providedIn: 'root',
})
export class TeachingSceneService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  copyToMine = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingSceneDto>({
      method: 'POST',
      url: `/api/app/teaching-scene/${id}/copy-to-mine`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateTeachingSceneDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingSceneDto>({
      method: 'POST',
      url: '/api/app/teaching-scene',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/teaching-scene/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (category?: TeachingSceneCategory, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingSceneDto[]>({
      method: 'GET',
      url: '/api/app/teaching-scene',
      params: { category },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateTeachingSceneDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingSceneDto>({
      method: 'PUT',
      url: `/api/app/teaching-scene/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}