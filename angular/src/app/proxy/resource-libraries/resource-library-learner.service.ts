import type { GetResourceLibraryLearnersInput, RegisterOtherLearnerInput, ResourceLibraryLearnerDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ResourceLibraryLearnerService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  exportLearners = (input: GetResourceLibraryLearnersInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'POST',
      url: '/api/app/resource-library-learner/export-learners',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getLearners = (input: GetResourceLibraryLearnersInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceLibraryLearnerDto>>({
      method: 'GET',
      url: '/api/app/resource-library-learner/learners',
      params: { libraryId: input.libraryId, source: input.source, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyProfile = (libraryId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryLearnerDto>({
      method: 'GET',
      url: `/api/app/resource-library-learner/my-profile/${libraryId}`,
    },
    { apiName: this.apiName,...config });
  

  register = (input: RegisterOtherLearnerInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryLearnerDto>({
      method: 'POST',
      url: '/api/app/resource-library-learner/register',
      body: input,
    },
    { apiName: this.apiName,...config });
}