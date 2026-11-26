import type { CourseRecommendationOverviewDto, RecommendationRefreshResultDto, StudentRecommendationDto } from './dtos/models';
import type { StudentRecommendationStatus } from './enums/student-recommendation-status.enum';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StudentRecommendationService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getCourseOverview = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseRecommendationOverviewDto>({
      method: 'GET',
      url: `/api/app/student-recommendation/course-overview/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getMy = (courseId?: string, includeHandled?: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentRecommendationDto[]>({
      method: 'GET',
      url: '/api/app/student-recommendation/my',
      params: { courseId, includeHandled },
    },
    { apiName: this.apiName,...config });
  

  mark = (id: string, status: StudentRecommendationStatus, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentRecommendationDto>({
      method: 'POST',
      url: `/api/app/student-recommendation/${id}/mark`,
      params: { status },
    },
    { apiName: this.apiName,...config });
  

  refreshCourse = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecommendationRefreshResultDto>({
      method: 'POST',
      url: `/api/app/student-recommendation/refresh-course/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  refreshMine = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecommendationRefreshResultDto>({
      method: 'POST',
      url: `/api/app/student-recommendation/refresh-mine/${courseId}`,
    },
    { apiName: this.apiName,...config });
}