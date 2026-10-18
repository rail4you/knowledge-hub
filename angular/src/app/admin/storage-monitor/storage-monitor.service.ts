import { Injectable, inject } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import { PagedResultDto } from '@abp/ng.core';

/** 对应后端 StorageOverviewDto。 */
export interface StorageOverviewDto {
  localUsedBytes: number;
  localFileCount: number;
  localRootPath: string;
  localAvailable: boolean;
  ossAvailable: boolean;
  ossBucketName: string;
  ossUsedBytes: number;
  ossObjectCount: number;
  ossError?: string;
  totalResourceBytes: number;
  totalResourceFiles: number;
  recent30dBytes: number;
  recent30dFiles: number;
  tenantCount: number;
  tenantsInUse: number;
  defaultQuotaBytes: number;
  warnPercent: number;
  statisticsNote: string;
  generatedAt: string;
}

/** 对应后端 TenantStorageUsageDto。 */
export interface TenantStorageUsageDto {
  tenantId: string;
  tenantName: string;
  fileCount: number;
  usedBytes: number;
  quotaBytes: number;
  usagePercent: number;
  /** normal | warn | exceeded | unlimited | unused */
  status: string;
  recent30dBytes: number;
  recent30dFiles: number;
  lastUploadTime?: string;
}

export interface GetStorageMonitorListDto {
  skipCount: number;
  maxResultCount: number;
  sorting?: string;
  keyword?: string;
  statusFilter?: string;
}

export interface UpdateStorageQuotaDto {
  tenantId: string;
  quotaBytes: number;
}

export interface UpdateDefaultQuotaDto {
  quotaBytes: number;
  warnPercent: number;
}

export type PagedTenantStorageDto = PagedResultDto<TenantStorageUsageDto> & { items: TenantStorageUsageDto[] };

/**
 * 存储容量监控服务（host 全局管理员）。
 *
 * 对应后端 IStorageMonitorAppService 的约定式路由：
 *   GET  /api/app/storage-monitor/overview
 *   GET  /api/app/storage-monitor/tenant-list
 *   PUT  /api/app/storage-monitor/quota
 *   PUT  /api/app/storage-monitor/default
 */
@Injectable({ providedIn: 'root' })
export class StorageMonitorService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';

  getOverview = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, StorageOverviewDto>(
      { method: 'GET', url: '/api/app/storage-monitor/overview' },
      { apiName: this.apiName, ...config },
    );

  getTenantList = (input: GetStorageMonitorListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedTenantStorageDto>(
      { method: 'GET', url: '/api/app/storage-monitor/tenant-list', params: input },
      { apiName: this.apiName, ...config },
    );

  updateQuota = (input: UpdateStorageQuotaDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantStorageUsageDto>(
      { method: 'PUT', url: '/api/app/storage-monitor/quota', body: input },
      { apiName: this.apiName, ...config },
    );

  updateDefault = (input: UpdateDefaultQuotaDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StorageOverviewDto>(
      { method: 'PUT', url: '/api/app/storage-monitor/default', body: input },
      { apiName: this.apiName, ...config },
    );
}
