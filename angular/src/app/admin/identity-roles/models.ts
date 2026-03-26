export interface IdentityRoleDto {
  id?: string;
  name?: string;
  isDefault?: boolean;
  isPublic?: boolean;
  concurrencyStamp?: string;
  extraProperties?: Record<string, any>;
  tenantId?: string;
  isStatic?: boolean;
}

 export interface GetIdentityRolesInput {
  filter?: string;
    sorting?: string;
    skipCount?: number;
    maxResultCount?: number;
    tenantId?: string;
}

export interface IdentityRoleCreateDto {
  name?: string;
  isDefault?: boolean;
  isPublic?: boolean;
  roleCount?: number;
  tenantId?: string;
}

export interface IdentityRoleUpdateDto {
  name?: string;
  isDefault?: boolean;
  isPublic?: boolean;
  concurrencyStamp?: string;
}
