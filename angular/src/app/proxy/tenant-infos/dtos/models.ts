import type { FullAuditedEntityDto } from '@abp/ng.core';

export enum TenantType {
  Professional = 0,
  Project = 1,
}

export interface SpecialProjectItem {
  title: string;
  description?: string | null;
}

export interface TenantInfoDto extends FullAuditedEntityDto<string> {
  tenantId: string;
  type: TenantType;
  name: string;
  description?: string | null;
  coverImageList: string[];
  talentTrainingPlan?: string | null;
  professionalTeachingStandards?: string | null;
  specialProjectList: SpecialProjectItem[];
  majorCount: number;
  courseCount: number;
}

export interface CreateUpdateTenantInfoDto {
  name: string;
  type: TenantType;
  description?: string | null;
  coverImageList: string[];
  talentTrainingPlan?: string | null;
  professionalTeachingStandards?: string | null;
  specialProjectList: SpecialProjectItem[];
}

export interface TenantGraphNodeDto {
  id: string;
  name: string;
  nodeType: string;
  description?: string | null;
  childrenCount: number;
}

export interface TenantGraphRelationDto {
  sourceId: string;
  targetId: string;
  relationType: string;
  label?: string | null;
}

export interface TenantKnowledgeGraphDto {
  centerNode: TenantGraphNodeDto;
  majors: TenantGraphNodeDto[];
  allNodes: TenantGraphNodeDto[];
  relations: TenantGraphRelationDto[];
}
