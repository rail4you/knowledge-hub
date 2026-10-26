import type { AttachLibraryContentDto, CreateTeachingLibraryCourseDto, CreateTeachingLibraryPracticumDto, CreateTeachingLibraryResourceDto, TeachingLibraryContentDto, TeachingLibraryContentListRequestDto, TeachingLibraryDetailDto, TeachingLibraryDto, TeachingLibraryListRequestDto } from './dtos/models';
import type { LibraryContentType } from './library-content-type.enum';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TeachingResourceLibraryService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  attach = (id: string, input: AttachLibraryContentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingLibraryContentDto>({
      method: 'POST',
      url: `/api/app/teaching-resource-library/${id}/attach`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createCourse = (id: string, input: CreateTeachingLibraryCourseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingLibraryContentDto>({
      method: 'POST',
      url: `/api/app/teaching-resource-library/${id}/course`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createPracticum = (id: string, input: CreateTeachingLibraryPracticumDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingLibraryContentDto>({
      method: 'POST',
      url: `/api/app/teaching-resource-library/${id}/practicum`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createResource = (id: string, input: CreateTeachingLibraryResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingLibraryContentDto>({
      method: 'POST',
      url: `/api/app/teaching-resource-library/${id}/resource`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  detach = (id: string, contentType: LibraryContentType, contentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/teaching-resource-library/${id}/detach/${contentId}`,
      params: { contentType },
    },
    { apiName: this.apiName,...config });
  

  getContents = (id: string, input: TeachingLibraryContentListRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TeachingLibraryContentDto>>({
      method: 'GET',
      url: `/api/app/teaching-resource-library/${id}/contents`,
      params: { contentType: input.contentType, filter: input.filter, onlyMine: input.onlyMine, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getLibrary = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingLibraryDetailDto>({
      method: 'GET',
      url: `/api/app/teaching-resource-library/${id}/library`,
    },
    { apiName: this.apiName,...config });
  

  getMyLibraries = (input: TeachingLibraryListRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingLibraryDto[]>({
      method: 'GET',
      url: '/api/app/teaching-resource-library/my-libraries',
      params: { filter: input.filter, visibility: input.visibility, onlyShared: input.onlyShared },
    },
    { apiName: this.apiName,...config });
}