import type { GetTrackingResourcesInput, ResourceTrackingResourceDto, ResourceTrackingTimelineDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ResourceTrackingService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getList = (input: GetTrackingResourcesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceTrackingResourceDto>>({
      method: 'GET',
      url: '/api/app/resource-tracking',
      params: { filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTimeline = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceTrackingTimelineDto>({
      method: 'GET',
      url: `/api/app/resource-tracking/timeline/${resourceId}`,
    },
    { apiName: this.apiName,...config });
}