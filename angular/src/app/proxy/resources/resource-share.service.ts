import type { CreateResourceShareDto, ResourceShareDto, SharedResourceDto, SharedResourceListQueryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ResourceShareService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';

  getSharedByMe = (input: SharedResourceListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SharedResourceDto>>({
      method: 'GET',
      url: '/api/app/resource-share/shared-by-me',
      params: {
        filter: input.filter,
        resourceType: input.resourceType,
        categoryId: input.categoryId,
        majorId: input.majorId,
        sorting: input.sorting,
        skipCount: input.skipCount,
        maxResultCount: input.maxResultCount,
      },
    }, { apiName: this.apiName, ...config });

  getSharedToMe = (input: SharedResourceListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SharedResourceDto>>({
      method: 'GET',
      url: '/api/app/resource-share/shared-to-me',
      params: {
        filter: input.filter,
        resourceType: input.resourceType,
        categoryId: input.categoryId,
        majorId: input.majorId,
        sorting: input.sorting,
        skipCount: input.skipCount,
        maxResultCount: input.maxResultCount,
      },
    }, { apiName: this.apiName, ...config });

  getShares = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceShareDto[]>({
      method: 'GET',
      url: `/api/app/resource-share/${resourceId}/shares`,
    }, { apiName: this.apiName, ...config });

  share = (input: CreateResourceShareDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceShareDto[]>({
      method: 'POST',
      url: '/api/app/resource-share',
      body: input,
    }, { apiName: this.apiName, ...config });

  unshare = (resourceId: string, targetTenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource-share/${resourceId}/${targetTenantId}`,
    }, { apiName: this.apiName, ...config });
}
