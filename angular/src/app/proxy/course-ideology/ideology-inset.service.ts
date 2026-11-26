import type { CreateIdeologyInsetDto, IdeologyInsetDto, IdeologyRecommendItemDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IdeologyInsetService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateIdeologyInsetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyInsetDto>({
      method: 'POST',
      url: '/api/app/ideology-inset',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/ideology-inset/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByLessonPlan = (lessonPlanId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyInsetDto[]>({
      method: 'GET',
      url: `/api/app/ideology-inset/by-lesson-plan/${lessonPlanId}`,
    },
    { apiName: this.apiName,...config });
  

  recommend = (lessonPlanId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyRecommendItemDto[]>({
      method: 'POST',
      url: `/api/app/ideology-inset/recommend/${lessonPlanId}`,
    },
    { apiName: this.apiName,...config });
}