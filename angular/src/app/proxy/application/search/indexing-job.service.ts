import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IndexingJobDto } from '../contracts/search/dtos/models';
import type { CreateIndexingJobInput, GetIndexingJobsInput, TestParseResultDto } from '../contracts/search/models';

@Injectable({
  providedIn: 'root',
})
export class IndexingJobService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/indexing-job/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateIndexingJobInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexingJobDto>({
      method: 'POST',
      url: '/api/app/indexing-job',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexingJobDto>({
      method: 'GET',
      url: `/api/app/indexing-job/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByResourceId = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndexingJobDto>({
      method: 'GET',
      url: `/api/app/indexing-job/by-resource-id/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetIndexingJobsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IndexingJobDto>>({
      method: 'GET',
      url: '/api/app/indexing-job',
      params: { resourceId: input.resourceId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  retry = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/indexing-job/${id}/retry`,
    },
    { apiName: this.apiName,...config });
  

  retryAllFailed = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/indexing-job/retry-all-failed',
    },
    { apiName: this.apiName,...config });
  

  testExecuteJob = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      responseType: 'text',
      url: `/api/app/indexing-job/${id}/test-execute-job`,
    },
    { apiName: this.apiName,...config });
  

  testParse = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TestParseResultDto>({
      method: 'POST',
      url: `/api/app/indexing-job/test-parse/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  trigger = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/indexing-job/${id}/trigger`,
    },
    { apiName: this.apiName,...config });
}