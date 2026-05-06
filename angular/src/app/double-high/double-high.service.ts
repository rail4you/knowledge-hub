import { Injectable, inject } from '@angular/core';
import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Observable } from 'rxjs';

export enum DoubleHighProjectStatus {
  Draft = 0,
  Active = 1,
  Closed = 2,
}

export enum DoubleHighDataSourceType {
  Manual = 0,
  ResourceCount = 1,
  CourseCount = 2,
  MicroMajorCount = 3,
  PracticumProjectCount = 4,
  NewsArticleCount = 5,
  MicroMajorEnrollmentCount = 6,
  PracticumEnrollmentCount = 7,
}

export enum DoubleHighValueSourceType {
  Manual = 0,
  Automatic = 1,
}

export enum DoubleHighEvidenceType {
  ResourceLink = 0,
  AttachmentLink = 1,
  ExternalLink = 2,
}

export interface DoubleHighIndicatorValueSnapshotDto {
  indicatorId: string;
  value: number;
  note?: string;
  sourceType: DoubleHighValueSourceType;
  collectedAt: string;
}

export interface DoubleHighIndicatorDto {
  id: string;
  projectId: string;
  parentId?: string;
  categoryName: string;
  indicatorCode: string;
  name: string;
  description?: string;
  unit?: string;
  dataSourceType: DoubleHighDataSourceType;
  targetValue?: number;
  weight: number;
  sortOrder: number;
  latestValue?: DoubleHighIndicatorValueSnapshotDto;
}

export interface DoubleHighEvidenceDto {
  id: string;
  projectId: string;
  indicatorId: string;
  indicatorName?: string;
  title: string;
  description?: string;
  evidenceType: DoubleHighEvidenceType;
  resourceId?: string;
  resourceName?: string;
  attachmentUrl?: string;
  externalLink?: string;
  sortOrder: number;
  creationTime: string;
}

export interface DoubleHighReportDto {
  id: string;
  projectId: string;
  projectTitle?: string;
  reportName: string;
  summaryJson?: string;
  generatedById?: string;
  generatedByName?: string;
  generatedAt: string;
}

export interface DoubleHighDashboardDto {
  totalIndicators: number;
  manualIndicators: number;
  automaticIndicators: number;
  collectedIndicators: number;
  evidenceCount: number;
  completionRate: number;
  lastCollectedAt?: string;
}

export interface DoubleHighProjectDto {
  id: string;
  title: string;
  batchCode: string;
  description?: string;
  status: DoubleHighProjectStatus;
  startTime?: string;
  endTime?: string;
  lastCollectedAt?: string;
  indicatorCount: number;
  collectedIndicatorCount: number;
  evidenceCount: number;
  completionRate: number;
  creationTime: string;
}

export interface DoubleHighProjectDetailDto extends DoubleHighProjectDto {
  dashboard: DoubleHighDashboardDto;
  indicators: DoubleHighIndicatorDto[];
  evidences: DoubleHighEvidenceDto[];
  recentReports: DoubleHighReportDto[];
}

export interface CreateUpdateDoubleHighIndicatorDto {
  parentId?: string;
  categoryName: string;
  indicatorCode: string;
  name: string;
  description?: string;
  unit?: string;
  dataSourceType: DoubleHighDataSourceType;
  targetValue?: number;
  weight: number;
  sortOrder: number;
}

export interface CreateUpdateDoubleHighProjectDto {
  title: string;
  batchCode: string;
  description?: string;
  status: DoubleHighProjectStatus;
  startTime?: string;
  endTime?: string;
  indicators: CreateUpdateDoubleHighIndicatorDto[];
}

export interface CreateDoubleHighEvidenceDto {
  projectId: string;
  indicatorId: string;
  title: string;
  description?: string;
  evidenceType: DoubleHighEvidenceType;
  resourceId?: string;
  attachmentUrl?: string;
  externalLink?: string;
  sortOrder: number;
}

export interface SaveDoubleHighIndicatorValueDto {
  indicatorId: string;
  value: number;
  note?: string;
}

export interface PagedDoubleHighProjectRequestDto {
  filter?: string;
  status?: DoubleHighProjectStatus;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface GetDoubleHighReportsInput {
  projectId?: string;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

@Injectable({
  providedIn: 'root',
})
export class DoubleHighService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  get = (id: string) =>
    this.restService.request<any, DoubleHighProjectDto>({
      method: 'GET',
      url: `/api/app/double-high/${id}`,
    }, { apiName: this.apiName });

  getDetail = (id: string) =>
    this.restService.request<any, DoubleHighProjectDetailDto>({
      method: 'GET',
      url: `/api/app/double-high/${id}/detail`,
    }, { apiName: this.apiName });

  getList = (input: PagedDoubleHighProjectRequestDto) =>
    this.restService.request<any, PagedResultDto<DoubleHighProjectDto>>({
      method: 'GET',
      url: '/api/app/double-high',
      params: input,
    }, { apiName: this.apiName });

  create = (input: CreateUpdateDoubleHighProjectDto) =>
    this.restService.request<any, DoubleHighProjectDto>({
      method: 'POST',
      url: '/api/app/double-high',
      body: input,
    }, { apiName: this.apiName });

  update = (id: string, input: CreateUpdateDoubleHighProjectDto) =>
    this.restService.request<any, DoubleHighProjectDto>({
      method: 'PUT',
      url: `/api/app/double-high/${id}`,
      body: input,
    }, { apiName: this.apiName });

  delete = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/double-high/${id}`,
    }, { apiName: this.apiName });

  collectProject = (projectId: string) =>
    this.restService.request<any, DoubleHighDashboardDto>({
      method: 'POST',
      url: `/api/app/double-high/collect-project/${projectId}`,
    }, { apiName: this.apiName });

  saveManualValue = (input: SaveDoubleHighIndicatorValueDto) =>
    this.restService.request<any, DoubleHighIndicatorValueSnapshotDto>({
      method: 'POST',
      url: '/api/app/double-high/save-manual-value',
      body: input,
    }, { apiName: this.apiName });

  addEvidence = (input: CreateDoubleHighEvidenceDto) =>
    this.restService.request<any, DoubleHighEvidenceDto>({
      method: 'POST',
      url: '/api/app/double-high/evidence',
      body: input,
    }, { apiName: this.apiName });

  deleteEvidence = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/double-high/evidence/${id}`,
    }, { apiName: this.apiName });

  getReportList = (input: GetDoubleHighReportsInput) =>
    this.restService.request<any, PagedResultDto<DoubleHighReportDto>>({
      method: 'GET',
      url: '/api/app/double-high/report-list',
      params: input,
    }, { apiName: this.apiName });

  exportReport = (projectId: string, config?: Partial<Rest.Config>): Observable<Blob> =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/double-high/export-report/${projectId}`,
    }, { apiName: this.apiName, ...config });
}
