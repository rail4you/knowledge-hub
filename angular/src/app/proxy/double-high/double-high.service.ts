import type { CreateDoubleHighEvidenceDto, CreateUpdateDoubleHighIndicatorDto, CreateUpdateDoubleHighProjectDto, DoubleHighDashboardDto, DoubleHighEvidenceDto, DoubleHighIndicatorDto, DoubleHighIndicatorValueSnapshotDto, DoubleHighProjectDetailDto, DoubleHighProjectDto, DoubleHighReportDto, GetDoubleHighReportsInput, PagedDoubleHighProjectRequestDto, SaveDoubleHighIndicatorValueDto } from './dtos/models';
import type { DoubleHighDataSourceType } from './enums/double-high-data-source-type.enum';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DoubleHighService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  addEvidence = (input: CreateDoubleHighEvidenceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighEvidenceDto>({
      method: 'POST',
      url: '/api/app/double-high/evidence',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  addIndicator = (projectId: string, input: CreateUpdateDoubleHighIndicatorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighIndicatorDto>({
      method: 'POST',
      url: `/api/app/double-high/indicator/${projectId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  collectProject = (projectId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighDashboardDto>({
      method: 'POST',
      url: `/api/app/double-high/collect-project/${projectId}`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateDoubleHighProjectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighProjectDto>({
      method: 'POST',
      url: '/api/app/double-high',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/double-high/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteEvidence = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/double-high/${id}/evidence`,
    },
    { apiName: this.apiName,...config });
  

  deleteIndicator = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/double-high/${id}/indicator`,
    },
    { apiName: this.apiName,...config });
  

  downloadReport = (reportId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/double-high/download-report/${reportId}`,
    },
    { apiName: this.apiName,...config });
  

  exportReport = (projectId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/double-high/export-report/${projectId}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighProjectDto>({
      method: 'GET',
      url: `/api/app/double-high/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getDataSourcePreview = (dataSourceType: DoubleHighDataSourceType, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: '/api/app/double-high/data-source-preview',
      params: { dataSourceType },
    },
    { apiName: this.apiName,...config });
  

  getDetail = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighProjectDetailDto>({
      method: 'GET',
      url: `/api/app/double-high/${id}/detail`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedDoubleHighProjectRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DoubleHighProjectDto>>({
      method: 'GET',
      url: '/api/app/double-high',
      params: { filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getReportList = (input: GetDoubleHighReportsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DoubleHighReportDto>>({
      method: 'GET',
      url: '/api/app/double-high/report-list',
      params: { projectId: input.projectId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  saveManualValue = (input: SaveDoubleHighIndicatorValueDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighIndicatorValueSnapshotDto>({
      method: 'POST',
      url: '/api/app/double-high/save-manual-value',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateDoubleHighProjectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighProjectDto>({
      method: 'PUT',
      url: `/api/app/double-high/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateEvidence = (id: string, input: CreateDoubleHighEvidenceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighEvidenceDto>({
      method: 'PUT',
      url: `/api/app/double-high/${id}/evidence`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateIndicator = (id: string, input: CreateUpdateDoubleHighIndicatorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighIndicatorDto>({
      method: 'PUT',
      url: `/api/app/double-high/${id}/indicator`,
      body: input,
    },
    { apiName: this.apiName,...config });
}