import type { ExtensibleFullAuditedEntityDto, ExtensiblePagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface GetIdentityUsersInput extends ExtensiblePagedAndSortedResultRequestDto {
  filter?: string;
}

export interface IdentityUserDto extends ExtensibleFullAuditedEntityDto<string> {
  tenantId?: string | null;
  userName?: string;
  name?: string;
  surname?: string;
  email?: string;
  emailConfirmed?: boolean;
  phoneNumber?: string;
  phoneNumberConfirmed?: boolean;
  isActive?: boolean;
  lockoutEnabled?: boolean;
  accessFailedCount?: number;
  lockoutEnd?: string | null;
  concurrencyStamp?: string;
  entityVersion?: number;
  lastPasswordChangeTime?: string | null;
}
