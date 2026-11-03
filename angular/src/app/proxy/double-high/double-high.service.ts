import type { CreateDoubleHighEvidenceDto, CreateDoubleHighProjectFromTemplateDto, CreateUpdateDoubleHighIndicatorDto, CreateUpdateDoubleHighProjectDto, DoubleHighDashboardDto, DoubleHighEvidenceDto, DoubleHighIndicatorDto, DoubleHighIndicatorValueSnapshotDto, DoubleHighObjectOptionDto, DoubleHighProjectDetailDto, DoubleHighProjectDocumentDto, DoubleHighProjectDto, DoubleHighReportDto, DoubleHighTemplateDto, GetDoubleHighReportsInput, PagedDoubleHighProjectRequestDto, SaveDoubleHighIndicatorValueDto, SaveDoubleHighReportingDto } from './dtos/models';
import type { DoubleHighDataSourceType } from './enums/double-high-data-source-type.enum';
import type { DoubleHighObjectType } from './enums/double-high-object-type.enum';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

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
  

  addEvidenceFile = (projectId: string, indicatorId: string, evidenceName: string, file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighEvidenceDto>({
      method: 'POST',
      url: '/api/app/double-high/evidence-file',
      params: { projectId, indicatorId, evidenceName },
      body: file,
    },
    { apiName: this.apiName,...config });
  

  addIndicator = (projectId: string, input: CreateUpdateDoubleHighIndicatorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighIndicatorDto>({
      method: 'POST',
      url: `/api/app/double-high/indicator/${projectId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  addProjectDocument = (projectId: string, file: IFormFile, title: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighProjectDocumentDto>({
      method: 'POST',
      url: `/api/app/double-high/project-document/${projectId}`,
      params: { title },
      body: file,
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
  

  createFromTemplate = (input: CreateDoubleHighProjectFromTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighProjectDetailDto>({
      method: 'POST',
      url: '/api/app/double-high/from-template',
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
  

  deleteProjectDocument = (documentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/double-high/project-document/${documentId}`,
    },
    { apiName: this.apiName,...config });
  

  downloadEvidenceFile = (evidenceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/double-high/download-evidence-file/${evidenceId}`,
    },
    { apiName: this.apiName,...config });
  

  downloadProjectDocument = (documentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/double-high/download-project-document/${documentId}`,
    },
    { apiName: this.apiName,...config });
  

  downloadReport = (reportId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/double-high/download-report/${reportId}`,
    },
    { apiName: this.apiName,...config });
  

  exportAllPackage = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: '/api/app/double-high/export-all-package',
    },
    { apiName: this.apiName,...config });
  

  exportPackage = (projectId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/double-high/export-package/${projectId}`,
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
  

  getDataSourcePreview = (dataSourceType: DoubleHighDataSourceType, scopeType?: DoubleHighObjectType, scopeId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: '/api/app/double-high/data-source-preview',
      params: { dataSourceType, scopeType, scopeId },
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
  

  getObjectOptions = (objectType: DoubleHighObjectType, filter: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighObjectOptionDto[]>({
      method: 'GET',
      url: '/api/app/double-high/object-options',
      params: { objectType, filter },
    },
    { apiName: this.apiName,...config });
  

  getReportList = (input: GetDoubleHighReportsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DoubleHighReportDto>>({
      method: 'GET',
      url: '/api/app/double-high/report-list',
      params: { projectId: input.projectId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTemplates = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighTemplateDto[]>({
      method: 'GET',
      url: '/api/app/double-high/templates',
    },
    { apiName: this.apiName,...config });
  

  moveIndicator = (id: string, up: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/double-high/${id}/move-indicator`,
      params: { up },
    },
    { apiName: this.apiName,...config });
  

  saveManualValue = (input: SaveDoubleHighIndicatorValueDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighIndicatorValueSnapshotDto>({
      method: 'POST',
      url: '/api/app/double-high/save-manual-value',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  saveReporting = (input: SaveDoubleHighReportingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighIndicatorDto>({
      method: 'POST',
      url: '/api/app/double-high/save-reporting',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  startFilling = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighProjectDto>({
      method: 'POST',
      url: `/api/app/double-high/${id}/start-filling`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighProjectDto>({
      method: 'POST',
      url: `/api/app/double-high/${id}/submit`,
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