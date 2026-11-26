import type { CourseCaseDto, CourseCaseImportResultDto, CourseCaseQueryDto, CreateCourseCaseDto, UpdateCourseCaseDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

@Injectable({
  providedIn: 'root',
})
export class CourseCaseService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateCourseCaseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseCaseDto>({
      method: 'POST',
      url: '/api/app/course-case',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/course-case/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseCaseDto>({
      method: 'GET',
      url: `/api/app/course-case/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CourseCaseQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CourseCaseDto>>({
      method: 'GET',
      url: '/api/app/course-case',
      params: { courseId: input.courseId, chapterId: input.chapterId, source: input.source, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  importFromExcel = (courseId: string, file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseCaseImportResultDto>({
      method: 'POST',
      url: `/api/app/course-case/import-from-excel/${courseId}`,
      body: file,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateCourseCaseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseCaseDto>({
      method: 'PUT',
      url: `/api/app/course-case/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}