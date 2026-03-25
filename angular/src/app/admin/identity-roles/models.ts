export interface IdentityRoleDto {
  id?: string;
  name?: string;
  isDefault?: boolean;
  isPublic?: boolean;
  concurrencyStamp?: string;
  extraProperties?: Record<string, any>;
}

export interface GetIdentityRolesInput {
  filter?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

export interface IdentityRoleCreateDto {
  name?: string;
  isDefault?: boolean;
  isPublic?: boolean;
  roleCount?: number;
}

export interface IdentityRoleUpdateDto {
  name?: string;
  isDefault?: boolean;
  isPublic?: boolean;
  concurrencyStamp?: string;
}
