import type { CourseInsightDto, CourseLearningProfileDto, ProfileReportDto, StudentLearningProfileDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LearningProfileService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  buildReport = (courseId: string, template: string = "monthly", days: number = 30, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProfileReportDto>({
      method: 'POST',
      url: `/api/app/learning-profile/build-report/${courseId}`,
      params: { template, days },
    },
    { apiName: this.apiName,...config });
  

  exportReport = (courseId: string, template: string = "monthly", days: number = 30, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/learning-profile/export-report/${courseId}`,
      params: { template, days },
    },
    { apiName: this.apiName,...config });
  

  getCourseInsight = (courseId: string, days: number = 30, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseInsightDto>({
      method: 'GET',
      url: `/api/app/learning-profile/course-insight/${courseId}`,
      params: { days },
    },
    { apiName: this.apiName,...config });
  

  getCourseProfile = (courseId: string, days: number = 30, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseLearningProfileDto>({
      method: 'GET',
      url: `/api/app/learning-profile/course-profile/${courseId}`,
      params: { days },
    },
    { apiName: this.apiName,...config });
  

  getMyProfile = (courseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentLearningProfileDto>({
      method: 'GET',
      url: '/api/app/learning-profile/my-profile',
      params: { courseId },
    },
    { apiName: this.apiName,...config });
  

  getStudentProfile = (studentId: string, courseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentLearningProfileDto>({
      method: 'GET',
      url: '/api/app/learning-profile/student-profile',
      params: { studentId, courseId },
    },
    { apiName: this.apiName,...config });
}