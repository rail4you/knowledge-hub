import type { CooperationProjectStage } from '../cooperation-project-stage.enum';
import type { CooperationDemandType } from '../cooperation-demand-type.enum';
import type { CooperationDemandScope } from '../cooperation-demand-scope.enum';
import type { CooperationDemandStatus } from '../cooperation-demand-status.enum';
import type { PagedResultRequestDto } from '@abp/ng.core';
import type { CooperationResponseStatus } from '../cooperation-response-status.enum';

export interface AdvanceProjectStageDto {
  projectId?: string;
  stage?: CooperationProjectStage;
  acceptanceNote?: string | null;
}

export interface CompleteMilestoneDto {
  milestoneId?: string;
  evidenceRef?: string | null;
  note?: string | null;
}

export interface CooperationDemandDto {
  id?: string;
  enterpriseProfileId?: string;
  enterpriseName?: string | null;
  type?: CooperationDemandType;
  typeName?: string;
  title?: string;
  majors?: string | null;
  industries?: string | null;
  region?: string | null;
  background?: string | null;
  enterpriseInput?: string | null;
  schoolConditions?: string | null;
  cycle?: string | null;
  scale?: string | null;
  budgetNote?: string | null;
  deliveryAcceptance?: string | null;
  ipTerms?: string | null;
  contactName?: string | null;
  contactEmail?: string | null;
  scope?: CooperationDemandScope;
  status?: CooperationDemandStatus;
  statusName?: string;
  publishedAt?: string | null;
  rejectReason?: string | null;
  responseCount?: number;
  creationTime?: string;
  canEdit?: boolean;
}

export interface CooperationDemandListRequestDto extends PagedResultRequestDto {
  type?: CooperationDemandType | null;
  status?: CooperationDemandStatus | null;
  region?: string | null;
  major?: string | null;
  filter?: string | null;
  onlyMine?: boolean;
}

export interface CooperationProjectDto {
  id?: string;
  demandId?: string | null;
  responseId?: string | null;
  enterpriseProfileId?: string;
  enterpriseName?: string | null;
  schoolTenantId?: string;
  schoolTenantName?: string | null;
  name?: string;
  stage?: CooperationProjectStage;
  stageName?: string;
  agreementRef?: string | null;
  startAt?: string | null;
  endAt?: string | null;
  /** 项目台账创建时间 */
  creationTime?: string;
  /** 最近一个未完成里程碑的截止时间 */
  nextMilestoneDueAt?: string | null;
  /** 已逾期的未完成里程碑数量 */
  overdueMilestoneCount?: number;
  acceptanceNote?: string | null;
  milestoneCount?: number;
  completedMilestoneCount?: number;
  milestones?: ProjectMilestoneDto[];
}

export interface CooperationResponseDto {
  id?: string;
  demandId?: string;
  schoolTenantId?: string;
  schoolTenantName?: string | null;
  responderUserId?: string;
  responderName?: string | null;
  note?: string | null;
  status?: CooperationResponseStatus;
  statusName?: string;
  decidedAt?: string | null;
  decisionNote?: string | null;
  creationTime?: string;
  canDecide?: boolean;
}

export interface CreateCooperationProjectDto {
  responseId?: string | null;
  name?: string;
  agreementRef?: string | null;
  startAt?: string | null;
  endAt?: string | null;
}

export interface CreateCooperationResponseDto {
  demandId?: string;
  note?: string | null;
}

export interface CreateMilestoneDto {
  projectId?: string;
  name?: string;
  dueAt?: string | null;
  sortOrder?: number;
}

export interface CreateUpdateCooperationDemandDto {
  type?: CooperationDemandType;
  title?: string;
  majors?: string | null;
  industries?: string | null;
  region?: string | null;
  background?: string | null;
  enterpriseInput?: string | null;
  schoolConditions?: string | null;
  cycle?: string | null;
  scale?: string | null;
  budgetNote?: string | null;
  deliveryAcceptance?: string | null;
  ipTerms?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  scope?: CooperationDemandScope;
}

export interface DecideCooperationResponseDto {
  accept?: boolean;
  note?: string | null;
}

export interface ProjectMilestoneDto {
  id?: string;
  name?: string;
  dueAt?: string | null;
  completedAt?: string | null;
  evidenceRef?: string | null;
  note?: string | null;
  isCompleted?: boolean;
  /** 执行顺序 */
  sortOrder?: number;
  /** 读时计算的状态：已完成 / 已逾期 / 进行中 */
  statusName?: string;
  /** 是否已逾期 */
  isOverdue?: boolean;
}
