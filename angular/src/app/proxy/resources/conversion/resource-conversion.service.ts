import type { ConvertToCourseInput, ConvertToLibraryInput, CourseConvertibleResourceDto, CourseResourceQueryDto, LibraryConvertibleResourceDto, LibraryResourceQueryDto, ResourceConversionResultDto, ResourceLibraryOptionDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ResourceConversionService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  convertToCourse = (input: ConvertToCourseInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceConversionResultDto>({
      method: 'POST',
      url: '/api/app/resource-conversion/convert-to-course',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  convertToLibrary = (input: ConvertToLibraryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceConversionResultDto>({
      method: 'POST',
      url: '/api/app/resource-conversion/convert-to-library',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getCourseResources = (input: CourseResourceQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CourseConvertibleResourceDto>>({
      method: 'GET',
      url: '/api/app/resource-conversion/course-resources',
      params: { courseId: input.courseId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getLibraryResources = (input: LibraryResourceQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LibraryConvertibleResourceDto>>({
      method: 'GET',
      url: '/api/app/resource-conversion/library-resources',
      params: { libraryId: input.libraryId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyLibraries = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryOptionDto[]>({
      method: 'GET',
      url: '/api/app/resource-conversion/my-libraries',
    },
    { apiName: this.apiName,...config });
}