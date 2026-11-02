import type { CourseLessonPlanDto, CourseLessonPlanQueryDto, CreateCourseLessonPlanDto, UpdateCourseLessonPlanDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CourseLessonPlanService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateCourseLessonPlanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseLessonPlanDto>({
      method: 'POST',
      url: '/api/app/course-lesson-plan',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/course-lesson-plan/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseLessonPlanDto>({
      method: 'GET',
      url: `/api/app/course-lesson-plan/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CourseLessonPlanQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CourseLessonPlanDto>>({
      method: 'GET',
      url: '/api/app/course-lesson-plan',
      params: { courseId: input.courseId, chapterId: input.chapterId, source: input.source, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateCourseLessonPlanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseLessonPlanDto>({
      method: 'PUT',
      url: `/api/app/course-lesson-plan/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}