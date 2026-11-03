import { Injectable, inject } from '@angular/core';
import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Observable } from 'rxjs';

export enum DoubleHighProjectStatus {
  Draft = 0,
  Active = 1,
  Closed = 2,
  /** 已提交：教师完成填报并提交，内容锁定 */
  Submitted = 3,
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
  /** 课程选课人次 */
  CourseEnrollmentCount = 8,
  /** 课程学习完成率（%） */
  CourseCompletionRate = 9,
  /** 就业率（%） */
  EmploymentRate = 10,
}

export enum DoubleHighValueSourceType {
  Manual = 0,
  Automatic = 1,
}

/** 指标填报类型：系统取数 / 填写数值 / 填写说明 / 仅佐证 */
export enum DoubleHighIndicatorType {
  Auto = 0,
  Number = 1,
  Text = 2,
  EvidenceOnly = 3,
}

/** 指标评分规则：比例制 / 达标制 / 直接计分 / 不计分 */
export enum DoubleHighScoringRule {
  Proportional = 0,
  Threshold = 1,
  Direct = 2,
  None = 3,
}

export enum DoubleHighEvidenceType {
  ResourceLink = 0,
  AttachmentLink = 1,
  ExternalLink = 2,
}

export interface DoubleHighIndicatorValueSnapshotDto {
  indicatorId: string;
  value: number;
  textValue?: string;
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
  /** 填报类型 */
  indicatorType: DoubleHighIndicatorType;
  /** 评分规则 */
  scoringRule: DoubleHighScoringRule;
  /** 必备佐证清单 */
  requiredEvidences: string[];
  /** 设置依据 */
  basis?: string;
  /** 数据来源文字描述 */
  dataSourceName?: string;
  /** 完成度（0~100） */
  progress: number;
  /** 加权得分 */
  score: number;
  /** 是否已完成 */
  completed: boolean;
  /** 完成状态文案 */
  completionStatus: string;
  /** 已上传佐证数 */
  evidenceCount: number;
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
  /** 佐证名称（对应必备佐证清单中的某一项） */
  evidenceName?: string;
  /** 上传文件原始文件名 */
  fileName?: string;
  /** 上传文件大小（字节） */
  fileSize: number;
  /** 是否为教师上传的文件 */
  isFile: boolean;
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
  completedIndicators: number;
  evidenceCount: number;
  completionRate: number;
  totalScore: number;
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
  /** 评估模板 Key */
  templateKey?: string;
  /** 评估模板名称 */
  templateName?: string;
  /** 评估对象 */
  assessmentTarget?: string;
  /** 填报人 */
  reporterName?: string;
  /** 提交时间 */
  submittedAt?: string;
  /** 已完成指标数 */
  completedIndicatorCount: number;
  /** 加权总分 */
  totalScore: number;
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
  indicatorType?: DoubleHighIndicatorType;
  scoringRule?: DoubleHighScoringRule;
  requiredEvidences?: string[];
  basis?: string;
  dataSourceName?: string;
}

export interface CreateUpdateDoubleHighProjectDto {
  title: string;
  batchCode: string;
  description?: string;
  status: DoubleHighProjectStatus;
  startTime?: string;
  endTime?: string;
  templateKey?: string;
  assessmentTarget?: string;
  reporterName?: string;
  indicators: CreateUpdateDoubleHighIndicatorDto[];
}

/** 按模板创建评估项目 */
export interface CreateDoubleHighProjectFromTemplateDto {
  templateKey: string;
  title: string;
  batchCode?: string;
  description?: string;
  assessmentTarget?: string;
  reporterName?: string;
  startTime?: string;
  endTime?: string;
}

/** 教师填报内容 */
export interface SaveDoubleHighReportingDto {
  indicatorId: string;
  value?: number;
  textValue?: string;
  note?: string;
}

export interface DoubleHighTemplateIndicatorDto {
  name: string;
  indicatorType: DoubleHighIndicatorType;
  unit?: string;
  dataSourceType: DoubleHighDataSourceType;
  dataSourceName?: string;
  weight: number;
  scoringRule: DoubleHighScoringRule;
  targetValue?: number;
  requiredEvidences: string[];
  basis?: string;
}

export interface DoubleHighTemplateDto {
  key: string;
  name: string;
  scopeLabel: string;
  description?: string;
  indicators: DoubleHighTemplateIndicatorDto[];
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

  getDataSourcePreview = (dataSourceType: DoubleHighDataSourceType) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: '/api/app/double-high/data-source-preview',
      params: { dataSourceType },
    }, { apiName: this.apiName });

  addIndicator = (projectId: string, input: CreateUpdateDoubleHighIndicatorDto) =>
    this.restService.request<any, DoubleHighIndicatorDto>({
      method: 'POST',
      url: `/api/app/double-high/indicator/${projectId}`,
      body: input,
    }, { apiName: this.apiName });

  updateIndicator = (id: string, input: CreateUpdateDoubleHighIndicatorDto) =>
    this.restService.request<any, DoubleHighIndicatorDto>({
      method: 'PUT',
      url: `/api/app/double-high/${id}/indicator`,
      body: input,
    }, { apiName: this.apiName });

  deleteIndicator = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/double-high/${id}/indicator`,
    }, { apiName: this.apiName });

  moveIndicator = (id: string, up: boolean) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/double-high/${id}/move-indicator`,
      params: { up },
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

  updateEvidence = (id: string, input: CreateDoubleHighEvidenceDto) =>
    this.restService.request<any, DoubleHighEvidenceDto>({
      method: 'PUT',
      url: `/api/app/double-high/${id}/evidence`,
      body: input,
    }, { apiName: this.apiName });

  deleteEvidence = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/double-high/${id}/evidence`,
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

  downloadReport = (reportId: string, config?: Partial<Rest.Config>): Observable<Blob> =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/double-high/download-report/${reportId}`,
    }, { apiName: this.apiName, ...config });

  // ── 评估管理（教师填报流程）─────────────────────────────────────────

  /** 评估模板目录 */
  getTemplates = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoubleHighTemplateDto[]>({
      method: 'GET',
      url: '/api/app/double-high/templates',
    }, { apiName: this.apiName, ...config });

  /** 按模板创建评估项目 */
  createFromTemplate = (input: CreateDoubleHighProjectFromTemplateDto) =>
    this.restService.request<any, DoubleHighProjectDetailDto>({
      method: 'POST',
      url: '/api/app/double-high/from-template',
      body: input,
    }, { apiName: this.apiName });

  /** 开始填报（配置定稿） */
  startFilling = (projectId: string) =>
    this.restService.request<any, DoubleHighProjectDto>({
      method: 'POST',
      url: `/api/app/double-high/${projectId}/start-filling`,
    }, { apiName: this.apiName });

  /** 提交填报 */
  submit = (projectId: string) =>
    this.restService.request<any, DoubleHighProjectDto>({
      method: 'POST',
      url: `/api/app/double-high/${projectId}/submit`,
    }, { apiName: this.apiName });

  /** 保存教师填报内容 */
  saveReporting = (input: SaveDoubleHighReportingDto) =>
    this.restService.request<any, DoubleHighIndicatorDto>({
      method: 'POST',
      url: '/api/app/double-high/save-reporting',
      body: input,
    }, { apiName: this.apiName });

  /** 上传佐证文件 */
  addEvidenceFile = (projectId: string, indicatorId: string, evidenceName: string, file: File) => {
    // 后端签名为 AddEvidenceFileAsync(projectId, indicatorId, evidenceName, file)，
    // 含 IFormFile 时四个参数全部从表单读取。
    const formData = new FormData();
    formData.append('projectId', projectId);
    formData.append('indicatorId', indicatorId);
    formData.append('evidenceName', evidenceName ?? '');
    formData.append('file', file, file.name);
    return this.restService.request<any, DoubleHighEvidenceDto>({
      method: 'POST',
      url: '/api/app/double-high/evidence-file',
      body: formData as any,
    }, { apiName: this.apiName });
  };

  /** 下载佐证文件 */
  downloadEvidenceFile = (evidenceId: string, config?: Partial<Rest.Config>): Observable<Blob> =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/double-high/download-evidence-file/${evidenceId}`,
    }, { apiName: this.apiName, ...config });

  /** 导出单个项目的资料包（zip） */
  exportPackage = (projectId: string, config?: Partial<Rest.Config>): Observable<Blob> =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/double-high/export-package/${projectId}`,
    }, { apiName: this.apiName, ...config });

  /** 导出全部项目的资料包（zip） */
  exportAllPackage = (config?: Partial<Rest.Config>): Observable<Blob> =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: '/api/app/double-high/export-all-package',
    }, { apiName: this.apiName, ...config });
}
