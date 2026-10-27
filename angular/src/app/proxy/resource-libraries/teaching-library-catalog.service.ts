import type { TeachingLibraryCardDto, TeachingLibraryCatalogRequestDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TeachingLibraryCatalogService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getList = (input: TeachingLibraryCatalogRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TeachingLibraryCardDto>>({
      method: 'GET',
      url: '/api/app/teaching-library-catalog',
      params: { scope: input.scope, filter: input.filter, primaryMajorId: input.primaryMajorId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}