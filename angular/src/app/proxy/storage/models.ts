import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface GetStorageMonitorListDto extends PagedAndSortedResultRequestDto {
  keyword?: string | null;
  statusFilter?: string | null;
}

export interface StorageOverviewDto {
  localUsedBytes?: number;
  localFileCount?: number;
  localRootPath?: string;
  localAvailable?: boolean;
  ossAvailable?: boolean;
  ossBucketName?: string;
  ossUsedBytes?: number;
  ossObjectCount?: number;
  ossError?: string | null;
  totalResourceBytes?: number;
  totalResourceFiles?: number;
  recent30dBytes?: number;
  recent30dFiles?: number;
  tenantCount?: number;
  tenantsInUse?: number;
  defaultQuotaBytes?: number;
  warnPercent?: number;
  statisticsNote?: string;
  generatedAt?: string;
}

export interface TenantStorageUsageDto {
  tenantId?: string;
  tenantName?: string;
  fileCount?: number;
  usedBytes?: number;
  quotaBytes?: number;
  usagePercent?: number;
  status?: string;
  recent30dBytes?: number;
  recent30dFiles?: number;
  lastUploadTime?: string | null;
}

export interface UpdateDefaultQuotaDto {
  quotaBytes?: number;
  warnPercent?: number;
}

export interface UpdateStorageQuotaDto {
  tenantId?: string;
  quotaBytes?: number;
}
