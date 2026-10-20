import type { GetStorageMonitorListDto, StorageOverviewDto, TenantStorageUsageDto, UpdateDefaultQuotaDto, UpdateStorageQuotaDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StorageMonitorService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getOverview = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, StorageOverviewDto>({
      method: 'GET',
      url: '/api/app/storage-monitor/overview',
    },
    { apiName: this.apiName,...config });
  

  getTenantList = (input: GetStorageMonitorListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TenantStorageUsageDto>>({
      method: 'GET',
      url: '/api/app/storage-monitor/tenant-list',
      params: { keyword: input.keyword, statusFilter: input.statusFilter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  updateDefault = (input: UpdateDefaultQuotaDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StorageOverviewDto>({
      method: 'PUT',
      url: '/api/app/storage-monitor/default',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateQuota = (input: UpdateStorageQuotaDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantStorageUsageDto>({
      method: 'PUT',
      url: '/api/app/storage-monitor/quota',
      body: input,
    },
    { apiName: this.apiName,...config });
}