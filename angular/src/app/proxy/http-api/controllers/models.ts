
export interface AssignRoleRequestDto {
  tenantId?: string;
  userName?: string;
  roleName?: string;
}

export interface AssignRoleResultDto {
  tenantId?: string;
  userName?: string;
  roleName?: string;
  userId?: string;
  roleId?: string;
  success?: boolean;
  alreadyInRole?: boolean;
  error?: string | null;
}

export interface PermissionItem {
  name?: string;
  providerName?: string;
  providerKey?: string;
  isGranted?: boolean;
}

export interface ReseedRequestDto {
  tenantIds?: string[] | null;
}

export interface ReseedResultDto {
  successCount?: number;
  failureCount?: number;
  tenants?: TenantReseedResultDto[];
}

export interface RolePermissionDto {
  roleName?: string;
  grantedCount?: number;
  totalCount?: number;
  coursesCreate?: boolean;
  coursesEdit?: boolean;
  coursesDelete?: boolean;
  tenantInfoDefault?: boolean;
  tenantInfoEdit?: boolean;
}

export interface SetPermissionForTenantInput {
  tenantId?: string | null;
  permissions?: PermissionItem[];
}

export interface TenantInfoDto {
  id?: string;
  name?: string;
}

export interface TenantReseedResultDto {
  tenantId?: string;
  tenantName?: string;
  success?: boolean;
  error?: string | null;
}

export interface UserPermissionDiagnosticDto {
  tenantId?: string;
  userName?: string;
  userId?: string;
  roleNames?: string[];
  rolePermissions?: RolePermissionDto[];
  error?: string | null;
}

export interface WhoAmIDto {
  userId?: string;
  userName?: string;
  isAuthenticated?: boolean;
  tenantId?: string | null;
  roleNames?: string[];
  permissions?: Record<string, boolean>;
}
