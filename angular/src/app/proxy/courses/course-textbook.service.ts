import type { CourseTextbookDto, SaveCourseTextbookDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CourseTextbookService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: SaveCourseTextbookDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseTextbookDto>({
      method: 'POST',
      url: '/api/app/course-textbook',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/course-textbook/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByCourse = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseTextbookDto[]>({
      method: 'GET',
      url: `/api/app/course-textbook/by-course/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  setPrimary = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseTextbookDto>({
      method: 'POST',
      url: `/api/app/course-textbook/${id}/set-primary`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: SaveCourseTextbookDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseTextbookDto>({
      method: 'PUT',
      url: `/api/app/course-textbook/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}