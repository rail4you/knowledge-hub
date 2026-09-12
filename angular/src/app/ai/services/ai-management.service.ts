import { Injectable, inject } from '@angular/core';
import { RestService, type PagedResultDto } from '@abp/ng.core';

export interface AiModelPriceDto {
  model: string;
  inputPerMillion: number;
  outputPerMillion: number;
}

export interface AiManagementStatusDto {
  maskedApiKey: string;
  hasApiKey: boolean;
  textModel: string;
  visionModel: string;
  videoFps: number;
  pricing: AiModelPriceDto[];
}

export interface AiUsageRecordDto {
  id: string;
  tenantId?: string;
  tenantName?: string;
  userId: string;
  userName?: string;
  roles?: string;
  featureGroup: string;
  featureGroupName: string;
  feature: string;
  model: string;
  status: number;
  statusName: string;
  inputTokens: number;
  outputTokens: number;
  isEstimated: boolean;
  estimatedCost: number;
  errorMessage?: string;
  creationTime: string;
}

export interface GetAiUsageRecordsInput {
  startTime?: string;
  endTime?: string;
  featureGroup?: string;
  status?: number;
  filter?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface AiUsageSummaryDto {
  totalCount: number;
  successCount: number;
  failedCount: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalEstimatedCost: number;
}

export interface AiQuotasDto {
  quotas: Record<string, Record<string, number | null>>;
}

@Injectable({ providedIn: 'root' })
export class AiManagementService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getStatus = () =>
    this.restService.request<any, AiManagementStatusDto>(
      { method: 'GET', url: '/api/app/ai-management/status' },
      { apiName: this.apiName },
    );

  updateApiKey = (apiKey: string) =>
    this.restService.request<any, void>(
      { method: 'PUT', url: '/api/app/ai-management/api-key', body: { apiKey } },
      { apiName: this.apiName },
    );

  testConnection = () =>
    this.restService.request<any, boolean>(
      { method: 'POST', url: '/api/app/ai-management/test-connection', body: {} },
      { apiName: this.apiName },
    );

  getUsageRecords = (input: GetAiUsageRecordsInput) =>
    this.restService.request<any, PagedResultDto<AiUsageRecordDto>>(
      { method: 'GET', url: '/api/app/ai-management/usage-records', params: { ...input } },
      { apiName: this.apiName },
    );

  getUsageSummary = (input: GetAiUsageRecordsInput) =>
    this.restService.request<any, AiUsageSummaryDto>(
      { method: 'GET', url: '/api/app/ai-management/usage-summary', params: { ...input } },
      { apiName: this.apiName },
    );

  getQuotas = () =>
    this.restService.request<any, AiQuotasDto>(
      { method: 'GET', url: '/api/app/ai-management/quotas' },
      { apiName: this.apiName },
    );

  updateQuotas = (input: AiQuotasDto) =>
    this.restService.request<any, void>(
      { method: 'PUT', url: '/api/app/ai-management/quotas', body: input },
      { apiName: this.apiName },
    );
}
