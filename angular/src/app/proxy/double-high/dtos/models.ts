import type { DoubleHighEvidenceType } from '../enums/double-high-evidence-type.enum';
import type { DoubleHighObjectType } from '../enums/double-high-object-type.enum';
import type { DoubleHighDataSourceType } from '../enums/double-high-data-source-type.enum';
import type { DoubleHighIndicatorType } from '../enums/double-high-indicator-type.enum';
import type { DoubleHighScoringRule } from '../enums/double-high-scoring-rule.enum';
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
  evidenceName?: string | null;
  fileName?: string | null;
  fileSize?: number;
}

export interface CreateDoubleHighProjectFromTemplateDto {
  templateKey?: string;
  title?: string;
  batchCode?: string;
  description?: string | null;
  assessmentTarget?: string | null;
  reporterName?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  objectType?: DoubleHighObjectType;
  objectId?: string | null;
  objectName?: string | null;
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
  indicatorType?: DoubleHighIndicatorType | null;
  scoringRule?: DoubleHighScoringRule | null;
  requiredEvidences?: string[] | null;
  basis?: string | null;
  basisDocumentId?: string | null;
  dataSourceName?: string | null;
  scopeType?: DoubleHighObjectType | null;
  scopeId?: string | null;
  scopeName?: string | null;
}

export interface CreateUpdateDoubleHighProjectDto {
  title?: string;
  batchCode?: string;
  description?: string | null;
  status?: DoubleHighProjectStatus;
  startTime?: string | null;
  endTime?: string | null;
  templateKey?: string | null;
  assessmentTarget?: string | null;
  reporterName?: string | null;
  objectType?: DoubleHighObjectType;
  objectId?: string | null;
  objectName?: string | null;
  indicators?: CreateUpdateDoubleHighIndicatorDto[];
}

export interface DoubleHighDashboardDto {
  totalIndicators?: number;
  manualIndicators?: number;
  automaticIndicators?: number;
  collectedIndicators?: number;
  completedIndicators?: number;
  evidenceCount?: number;
  completionRate?: number;
  totalScore?: number;
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
  evidenceName?: string | null;
  fileName?: string | null;
  fileSize?: number;
  isFile?: boolean;
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
  indicatorType?: DoubleHighIndicatorType;
  scoringRule?: DoubleHighScoringRule;
  requiredEvidences?: string[];
  basis?: string | null;
  dataSourceName?: string | null;
  scopeType?: DoubleHighObjectType | null;
  scopeId?: string | null;
  scopeName?: string | null;
  scopeLabel?: string;
  basisDocumentId?: string | null;
  basisDocumentTitle?: string | null;
  basisDocumentUrl?: string | null;
  progress?: number;
  score?: number;
  completed?: boolean;
  completionStatus?: string;
  evidenceCount?: number;
}

export interface DoubleHighIndicatorValueSnapshotDto {
  indicatorId?: string;
  value?: number;
  textValue?: string | null;
  note?: string | null;
  sourceType?: DoubleHighValueSourceType;
  collectedAt?: string;
}

export interface DoubleHighObjectOptionDto {
  id?: string;
  name?: string;
  extra?: string | null;
}

export interface DoubleHighProjectDetailDto extends DoubleHighProjectDto {
  dashboard?: DoubleHighDashboardDto;
  indicators?: DoubleHighIndicatorDto[];
  evidences?: DoubleHighEvidenceDto[];
  recentReports?: DoubleHighReportDto[];
  documents?: DoubleHighProjectDocumentDto[];
}

export interface DoubleHighProjectDocumentDto extends FullAuditedEntityDto<string> {
  projectId?: string;
  title?: string;
  description?: string | null;
  fileName?: string | null;
  fileSize?: number;
  fileUrl?: string | null;
  sortOrder?: number;
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
  templateKey?: string | null;
  templateName?: string | null;
  assessmentTarget?: string | null;
  reporterName?: string | null;
  submittedAt?: string | null;
  objectType?: DoubleHighObjectType;
  objectId?: string | null;
  objectName?: string | null;
  completedIndicatorCount?: number;
  totalScore?: number;
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

export interface DoubleHighTemplateDto {
  key?: string;
  name?: string;
  scopeLabel?: string;
  description?: string | null;
  indicators?: DoubleHighTemplateIndicatorDto[];
}

export interface DoubleHighTemplateIndicatorDto {
  name?: string;
  indicatorType?: DoubleHighIndicatorType;
  unit?: string | null;
  dataSourceType?: DoubleHighDataSourceType;
  dataSourceName?: string | null;
  weight?: number;
  scoringRule?: DoubleHighScoringRule;
  targetValue?: number | null;
  requiredEvidences?: string[];
  basis?: string | null;
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

export interface SaveDoubleHighReportingDto {
  indicatorId?: string;
  value?: number | null;
  textValue?: string | null;
  note?: string | null;
}
