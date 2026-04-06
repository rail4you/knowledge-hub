import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { PageIndexSearchResultDto, ResourcePageIndexDto } from '../contracts/search/dtos/models';
import type { PageIndexSearchInput } from '../contracts/search/models';

@Injectable({
  providedIn: 'root',
})
export class PageIndexService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  generate = (resourceVersionId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourcePageIndexDto>({
      method: 'POST',
      url: `/api/app/page-index/generate/${resourceVersionId}`,
    },
    { apiName: this.apiName,...config });
  

  get = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourcePageIndexDto>({
      method: 'GET',
      url: '/api/app/page-index',
      params: { resourceId },
    },
    { apiName: this.apiName,...config });
  

  search = (input: PageIndexSearchInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageIndexSearchResultDto[]>({
      method: 'POST',
      url: '/api/app/page-index/search',
      body: input,
    },
    { apiName: this.apiName,...config });
}