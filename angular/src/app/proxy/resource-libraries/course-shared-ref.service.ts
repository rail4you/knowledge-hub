import type { CourseSharedRefAccessDto, CourseSharedRefDto, CourseSharedRefListRequestDto, CreateCourseSharedRefDto, ReportUsageEventDto, SharedRefStatisticsDto, UpgradeCourseSharedRefDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CourseSharedRefService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  checkAccess = (refId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseSharedRefAccessDto>({
      method: 'POST',
      url: `/api/app/course-shared-ref/check-access/${refId}`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateCourseSharedRefDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseSharedRefDto>({
      method: 'POST',
      url: '/api/app/course-shared-ref',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getByCourse = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseSharedRefDto[]>({
      method: 'GET',
      url: `/api/app/course-shared-ref/by-course/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CourseSharedRefListRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CourseSharedRefDto>>({
      method: 'GET',
      url: '/api/app/course-shared-ref',
      params: { courseId: input.courseId, status: input.status, direction: input.direction, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getStatistics = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SharedRefStatisticsDto>({
      method: 'GET',
      url: '/api/app/course-shared-ref/statistics',
    },
    { apiName: this.apiName,...config });
  

  reportUsage = (input: ReportUsageEventDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: '/api/app/course-shared-ref/report-usage',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  revoke = (id: string, reason?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseSharedRefDto>({
      method: 'POST',
      url: `/api/app/course-shared-ref/${id}/revoke`,
      params: { reason },
    },
    { apiName: this.apiName,...config });
  

  upgrade = (id: string, input: UpgradeCourseSharedRefDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseSharedRefDto>({
      method: 'POST',
      url: `/api/app/course-shared-ref/${id}/upgrade`,
      body: input,
    },
    { apiName: this.apiName,...config });
}