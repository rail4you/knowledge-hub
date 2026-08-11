import type { CourseResourceDto, CreateCourseResourceDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CourseResourceService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateCourseResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseResourceDto>({
      method: 'POST',
      url: '/api/app/course-resource',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/course-resource/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByCourse = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseResourceDto[]>({
      method: 'GET',
      url: `/api/app/course-resource/by-course/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getByResource = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseResourceDto[]>({
      method: 'GET',
      url: `/api/app/course-resource/by-resource/${resourceId}`,
    },
    { apiName: this.apiName,...config });
}