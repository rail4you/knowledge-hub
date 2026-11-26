import type { CourseIdeologySettingDto, SaveCourseIdeologySettingDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CourseIdeologySettingService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  get = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseIdeologySettingDto>({
      method: 'GET',
      url: '/api/app/course-ideology-setting',
      params: { courseId },
    },
    { apiName: this.apiName,...config });
  

  save = (input: SaveCourseIdeologySettingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseIdeologySettingDto>({
      method: 'POST',
      url: '/api/app/course-ideology-setting/save',
      body: input,
    },
    { apiName: this.apiName,...config });
}