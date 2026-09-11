import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { GetResourceMediaJobsInput, ResourceMediaJobDto } from '../../application/contracts/resources/media/models';

@Injectable({
  providedIn: 'root',
})
export class ResourceMediaJobService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/resource-media-job/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  getByResourceId = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceMediaJobDto>({
      method: 'GET',
      url: `/api/app/resource-media-job/by-resource-id/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetResourceMediaJobsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceMediaJobDto>>({
      method: 'GET',
      url: '/api/app/resource-media-job',
      params: { resourceId: input.resourceId, status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  retry = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/resource-media-job/${id}/retry`,
    },
    { apiName: this.apiName,...config });
  

  retryAllFailed = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/resource-media-job/retry-all-failed',
    },
    { apiName: this.apiName,...config });
}