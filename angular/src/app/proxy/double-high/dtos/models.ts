import type { DoubleHighEvidenceType } from '../enums/double-high-evidence-type.enum';
import type { DoubleHighDataSourceType } from '../enums/double-high-data-source-type.enum';
import type { DoubleHighProjectStatus } from '../enums/double-high-project-status.enum';
import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { DoubleHighValueSourceType } from '../enums/double-high-value-source-type.enum';

export interface CreateDoubleHighEvidenceDto {
  projectId?: string;
  indicatorId?: string;
  title?: string;
  description?: string | null;
  evidenceType?: DoubleHighEvidenceType;
  resourceId?: string | null;
  attachmentUrl?: string | null;
  externalLink?: string | null;
  sortOrder?: number;
}

export interface CreateUpdateDoubleHighIndicatorDto {
  parentId?: string | null;
  categoryName?: string;
  indicatorCode?: string;
  name?: string;
  description?: string | null;
  unit?: string | null;
  dataSourceType?: DoubleHighDataSourceType;
  targetValue?: number | null;
  weight?: number;
  sortOrder?: number;
}

export interface CreateUpdateDoubleHighProjectDto {
  title?: string;
  batchCode?: string;
  description?: string | null;
  status?: DoubleHighProjectStatus;
  startTime?: string | null;
  endTime?: string | null;
  indicators?: CreateUpdateDoubleHighIndicatorDto[];
}

export interface DoubleHighDashboardDto {
  totalIndicators?: number;
  manualIndicators?: number;
  automaticIndicators?: number;
  collectedIndicators?: number;
  evidenceCount?: number;
  completionRate?: number;
  lastCollectedAt?: string | null;
}

export interface DoubleHighEvidenceDto extends FullAuditedEntityDto<string> {
  projectId?: string;
  indicatorId?: string;
  indicatorName?: string | null;
  title?: string;
  description?: string | null;
  evidenceType?: DoubleHighEvidenceType;
  resourceId?: string | null;
  resourceName?: string | null;
  attachmentUrl?: string | null;
  externalLink?: string | null;
  sortOrder?: number;
}

export interface DoubleHighIndicatorDto extends EntityDto<string> {
  projectId?: string;
  parentId?: string | null;
  categoryName?: string;
  indicatorCode?: string;
  name?: string;
  description?: string | null;
  unit?: string | null;
  dataSourceType?: DoubleHighDataSourceType;
  targetValue?: number | null;
  weight?: number;
  sortOrder?: number;
  latestValue?: DoubleHighIndicatorValueSnapshotDto | null;
}

export interface DoubleHighIndicatorValueSnapshotDto {
  indicatorId?: string;
  value?: number;
  note?: string | null;
  sourceType?: DoubleHighValueSourceType;
  collectedAt?: string;
}

export interface DoubleHighProjectDetailDto extends DoubleHighProjectDto {
  dashboard?: DoubleHighDashboardDto;
  indicators?: DoubleHighIndicatorDto[];
  evidences?: DoubleHighEvidenceDto[];
  recentReports?: DoubleHighReportDto[];
}

export interface DoubleHighProjectDto extends FullAuditedEntityDto<string> {
  title?: string;
  batchCode?: string;
  description?: string | null;
  status?: DoubleHighProjectStatus;
  startTime?: string | null;
  endTime?: string | null;
  lastCollectedAt?: string | null;
  indicatorCount?: number;
  collectedIndicatorCount?: number;
  evidenceCount?: number;
  completionRate?: number;
}

export interface DoubleHighReportDto extends FullAuditedEntityDto<string> {
  projectId?: string;
  projectTitle?: string | null;
  reportName?: string;
  summaryJson?: string | null;
  generatedById?: string | null;
  generatedByName?: string | null;
  generatedAt?: string;
}

export interface GetDoubleHighReportsInput extends PagedAndSortedResultRequestDto {
  projectId?: string | null;
}

export interface PagedDoubleHighProjectRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  status?: DoubleHighProjectStatus | null;
}

export interface SaveDoubleHighIndicatorValueDto {
  indicatorId?: string;
  value?: number;
  note?: string | null;
}
