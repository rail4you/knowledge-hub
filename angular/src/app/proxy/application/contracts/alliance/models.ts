import type { AuditedEntityDto, EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AuditStatus } from '../../../resources/enums/audit-status.enum';
import type { AllianceStatus } from '../../../alliance/enums/alliance-status.enum';
import type { AllianceMemberRole } from '../../../alliance/enums/alliance-member-role.enum';

export interface AllianceAuditDto extends EntityDto<string> {
  allianceId?: string;
  resourceId?: string;
  resourceName?: string;
  approverTenantId?: string;
  approverTenantName?: string;
  status?: AuditStatus;
  comment?: string | null;
}

export interface AllianceAuditInputDto {
  resourceId?: string;
  status?: AuditStatus;
  comment?: string | null;
}

export interface AllianceDto extends AuditedEntityDto<string> {
  name?: string;
  description?: string | null;
  status?: AllianceStatus;
  memberCount?: number;
}

export interface AllianceMemberDto extends EntityDto<string> {
  allianceId?: string;
  memberTenantId?: string;
  tenantName?: string;
  role?: AllianceMemberRole;
  joinedTime?: string;
}

export interface AllianceMemberQueryDto extends PagedAndSortedResultRequestDto {
  allianceId?: string | null;
}

export interface CreateAllianceDto {
  name?: string;
  description?: string | null;
}

export interface CreateAllianceMemberDto {
  allianceId?: string;
  tenantId?: string;
  tenantName?: string;
  role?: AllianceMemberRole;
}

export interface PendingAllianceAuditDto {
  resourceId?: string;
  resourceName?: string;
  submitterTenantId?: string;
  submitterTenantName?: string;
  submittedTime?: string;
}
