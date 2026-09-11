import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

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

export interface UserImportResultDto {
  totalCount?: number;
  successCount?: number;
  failCount?: number;
  failItems?: UserImportFailItemDto[];
}
