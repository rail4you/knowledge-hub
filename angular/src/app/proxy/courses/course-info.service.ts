import type { CourseInfoDto, SaveCourseInfoDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CourseInfoService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getByCourse = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseInfoDto>({
      method: 'GET',
      url: `/api/app/course-info/by-course/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  save = (input: SaveCourseInfoDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseInfoDto>({
      method: 'POST',
      url: '/api/app/course-info/save',
      body: input,
    },
    { apiName: this.apiName,...config });
}