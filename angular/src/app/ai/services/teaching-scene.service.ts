import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';

/** 与后端 KnowledgeHub.AI.TeachingSceneCategory 对齐 */
export enum TeachingSceneCategory {
  Image = 0,
  VideoScene = 10,
  VideoMotion = 20,
}

export interface TeachingScene {
  id: string;
  name: string;
  prompt: string;
  category: TeachingSceneCategory;
  sortOrder: number;
  isSystem: boolean;
}

export interface CreateUpdateTeachingScene {
  name: string;
  prompt: string;
  category: TeachingSceneCategory;
  sortOrder: number;
}

@Injectable({ providedIn: 'root' })
export class TeachingSceneService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getList = (category?: TeachingSceneCategory) =>
    this.restService.request<any, TeachingScene[]>(
      {
        method: 'GET',
        url: '/api/app/teaching-scene',
        params: category !== undefined && category !== null ? { category } : {},
      },
      { apiName: this.apiName },
    );

  create = (input: CreateUpdateTeachingScene) =>
    this.restService.request<any, TeachingScene>(
      { method: 'POST', url: '/api/app/teaching-scene', body: input },
      { apiName: this.apiName },
    );

  update = (id: string, input: CreateUpdateTeachingScene) =>
    this.restService.request<any, TeachingScene>(
      { method: 'PUT', url: `/api/app/teaching-scene/${id}`, body: input },
      { apiName: this.apiName },
    );

  delete = (id: string) =>
    this.restService.request<any, void>(
      { method: 'DELETE', url: `/api/app/teaching-scene/${id}` },
      { apiName: this.apiName },
    );

  copyToMine = (id: string) =>
    this.restService.request<any, TeachingScene>(
      { method: 'POST', url: `/api/app/teaching-scene/${id}/copy-to-mine` },
      { apiName: this.apiName },
    );
}
