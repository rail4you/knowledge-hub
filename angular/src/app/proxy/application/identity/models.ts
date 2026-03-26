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

export interface IdentityUserWithRolesDto {
  id?: string;
  tenantId?: string;
  userName?: string;
  name?: string;
  email?: string;
  isActive?: boolean;
  roleNames?: string[];
}
