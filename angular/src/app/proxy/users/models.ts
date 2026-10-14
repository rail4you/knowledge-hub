import type { EntityDto } from '@abp/ng.core';
import type { UserRoleType } from './user-role-type.enum';
import type { UserImportItemStatus } from './user-import-item-status.enum';

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

/** ABP Account ProfileController.ChangePasswordAsyncByInput 的请求体（Volo.Abp.Account.ChangePasswordInput） */
export interface UpdateMyProfileDto {
  email?: string | null;
  phoneNumber?: string | null;
}

/** 修改密码请求体（Volo.Abp.Account.ChangePasswordInput） */
export interface ChangeMyPasswordDto {
  currentPassword?: string;
  newPassword?: string;
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
