import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CreateTenantRoleDto {
  tenantId?: string | null;
  name?: string;
  isDefault?: boolean;
  isPublic?: boolean;
}

export interface CreateTenantUserDto {
  tenantId?: string | null;
  userName: string;
  emailAddress: string;
  password: string;
  name?: string;
  surname?: string;
  isActive?: boolean;
  roleNames?: string[];
}

export interface GetTenantRolesInput extends PagedAndSortedResultRequestDto {
  tenantId?: string | null;
  filter?: string | null;
}

export interface TenantRoleDto extends EntityDto<string> {
  name?: string;
  isDefault?: boolean;
  isStatic?: boolean;
  isPublic?: boolean;
  tenantId?: string | null;
  concurrencyStamp?: string | null;
}

export interface UpdateTenantRoleDto {
  name?: string;
  isDefault?: boolean;
  isPublic?: boolean;
  concurrencyStamp?: string | null;
}

export interface UpdateTenantUserDto {
  tenantId?: string | null;
  userName: string;
  email: string;
  name?: string;
  surname?: string;
  emailConfirmed?: boolean;
  phoneNumber?: string | null;
  phoneNumberConfirmed?: boolean;
  isActive?: boolean;
  twoFactorEnabled?: boolean;
  lockoutEnabled?: boolean;
  accessFailedCount?: number;
  className?: string | null;
  companyName?: string | null;
  course?: string | null;
  department?: string | null;
  employeeNumber?: string | null;
  grade?: string | null;
  industry?: string | null;
  major?: string | null;
  managementScope?: string | null;
  partnerSchool?: string | null;
  position?: string | null;
  remark?: string | null;
  roleType?: number;
  schoolId?: string | null;
  studentNumber?: string | null;
  title?: string | null;
  unifiedSocialCreditCode?: string | null;
  roleNames?: string[];
}
