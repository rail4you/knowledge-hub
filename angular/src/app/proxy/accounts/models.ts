import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AccountValidityStatus } from './account-validity-status.enum';

export interface AccountValidityDto extends EntityDto<string> {
  userId?: string;
  tenantId?: string | null;
  tenantName?: string | null;
  userName?: string;
  displayName?: string | null;
  roleName?: string;
  validUntil?: string | null;
  status?: AccountValidityStatus | null;
  isExpired?: boolean;
  isExpiringSoon?: boolean;
  remainingDays?: number | null;
  userIsActive?: boolean;
  lastModifiedTime?: string | null;
}

export interface GetAccountValidityListInput extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  tenantId?: string | null;
  roleName?: string | null;
  status?: AccountValidityStatus | null;
  expiringSoon?: boolean | null;
}

export interface SetAccountValidityBatchInput {
  userIds?: string[];
  validUntil?: string | null;
  permanent?: boolean;
  days?: number | null;
}

export interface SetAccountValidityInput {
  userId?: string;
  validUntil?: string | null;
  permanent?: boolean;
  days?: number | null;
}
