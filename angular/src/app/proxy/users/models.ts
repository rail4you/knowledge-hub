import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { UserRoleType } from './user-role-type.enum';
import type { UserImportItemStatus } from './user-import-item-status.enum';

export interface CreateUserDto {
  name: string;
  birthDate: string;
  shortBio?: string | null;
}

export interface GetUserListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface ImportUsersFileDto {
  fileBase64?: string;
  fileName?: string | null;
  tenantId?: string | null;
  overwriteExisting?: boolean;
}

export interface MyProfileDto extends EntityDto<string> {
  userName?: string;
  name?: string;
  email?: string | null;
  phoneNumber?: string | null;
  emailConfirmed?: boolean;
  phoneNumberConfirmed?: boolean;
}

export interface RolePermissionSummaryDto {
  roleName?: string;
  displayName?: string;
  grantedPermissionCount?: number;
  isGlobal?: boolean;
  highlightPermissions?: string[];
}

export interface UpdateMyProfileDto {
  email?: string | null;
  phoneNumber?: string | null;
}

export interface UpdateUserDto {
  name: string;
  birthDate: string;
  shortBio?: string | null;
}

export interface UserDto extends EntityDto<string> {
  name?: string;
  birthDate?: string;
  shortBio?: string;
}

export interface UserImportFailItemDto {
  rowNumber?: number;
  userName?: string;
  reason?: string;
}

export interface UserImportPreviewItemDto {
  rowNumber?: number;
  roleType?: UserRoleType;
  roleDisplayName?: string;
  userName?: string;
  name?: string;
  phoneNumber?: string;
  email?: string;
  tenantName?: string | null;
  status?: UserImportItemStatus;
  reason?: string | null;
  existingUserId?: string | null;
}

export interface UserImportResultDto {
  totalCount?: number;
  newCount?: number;
  overwriteCount?: number;
  skipCount?: number;
  failCount?: number;
  items?: UserImportPreviewItemDto[];
  failItems?: UserImportFailItemDto[];
}
