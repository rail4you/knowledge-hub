import type { AiManagementStatusDto, AiQuotasDto, AiUsageRecordDto, AiUsageSummaryDto, GetAiUsageRecordsInput, UpdateAiApiKeyDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AiManagementService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getQuotas = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, AiQuotasDto>({
      method: 'GET',
      url: '/api/app/ai-management/quotas',
    },
    { apiName: this.apiName,...config });
  

  getStatus = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, AiManagementStatusDto>({
      method: 'GET',
      url: '/api/app/ai-management/status',
    },
    { apiName: this.apiName,...config });
  

  getUsageRecords = (input: GetAiUsageRecordsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AiUsageRecordDto>>({
      method: 'GET',
      url: '/api/app/ai-management/usage-records',
      params: { startTime: input.startTime, endTime: input.endTime, featureGroup: input.featureGroup, status: input.status, filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getUsageSummary = (input: GetAiUsageRecordsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AiUsageSummaryDto>({
      method: 'GET',
      url: '/api/app/ai-management/usage-summary',
      params: { startTime: input.startTime, endTime: input.endTime, featureGroup: input.featureGroup, status: input.status, filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  testConnection = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: '/api/app/ai-management/test-connection',
    },
    { apiName: this.apiName,...config });
  

  updateApiKey = (input: UpdateAiApiKeyDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: '/api/app/ai-management/api-key',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateQuotas = (input: AiQuotasDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: '/api/app/ai-management/quotas',
      body: input,
    },
    { apiName: this.apiName,...config });
}