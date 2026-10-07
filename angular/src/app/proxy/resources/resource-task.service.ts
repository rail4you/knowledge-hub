import type { GetResourceTaskGroupsInput, ResourceTaskGroupDto, RetryTrackingTaskInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ResourceTaskService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getGroupedList = (input: GetResourceTaskGroupsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceTaskGroupDto>>({
      method: 'GET',
      url: '/api/app/resource-task/grouped-list',
      params: { filter: input.filter, resourceId: input.resourceId, resourceStatus: input.resourceStatus, taskStatus: input.taskStatus, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  retry = (input: RetryTrackingTaskInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/resource-task/retry',
      body: input,
    },
    { apiName: this.apiName,...config });
}