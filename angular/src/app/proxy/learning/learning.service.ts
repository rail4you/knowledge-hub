import type { KnowledgeMasteryDto, LearningDashboardDto, LearningProgressDto, RecordProgressInput, StudentCourseDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LearningService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getDashboard = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, LearningDashboardDto>({
      method: 'GET',
      url: '/api/app/learning/dashboard',
    },
    { apiName: this.apiName,...config });
  

  getKnowledgeMastery = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeMasteryDto[]>({
      method: 'GET',
      url: `/api/app/learning/knowledge-mastery/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getMyCourses = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentCourseDto[]>({
      method: 'GET',
      url: '/api/app/learning/my-courses',
    },
    { apiName: this.apiName,...config });
  

  getProgress = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LearningProgressDto>({
      method: 'GET',
      url: `/api/app/learning/progress/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  recordProgress = (input: RecordProgressInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LearningProgressDto>({
      method: 'POST',
      url: '/api/app/learning/record-progress',
      body: input,
    },
    { apiName: this.apiName,...config });
}