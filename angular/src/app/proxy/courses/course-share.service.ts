import type { CourseShareDto, CreateCourseShareDto, SharedCourseDto, SharedCourseListQueryDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CourseShareService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getSharedByMe = (input: SharedCourseListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SharedCourseDto>>({
      method: 'GET',
      url: '/api/app/course-share/shared-by-me',
      params: { filter: input.filter, majorId: input.majorId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getSharedToMe = (input: SharedCourseListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SharedCourseDto>>({
      method: 'GET',
      url: '/api/app/course-share/shared-to-me',
      params: { filter: input.filter, majorId: input.majorId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getShares = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseShareDto[]>({
      method: 'GET',
      url: `/api/app/course-share/shares/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  share = (input: CreateCourseShareDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseShareDto[]>({
      method: 'POST',
      url: '/api/app/course-share/share',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  shareToPublicSchool = (courseId: string, note: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseShareDto>({
      method: 'POST',
      url: `/api/app/course-share/share-to-public-school/${courseId}`,
      params: { note },
    },
    { apiName: this.apiName,...config });
  

  unshare = (courseId: string, targetTenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/course-share/unshare',
      params: { courseId, targetTenantId },
    },
    { apiName: this.apiName,...config });
}