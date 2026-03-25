export interface CreateTenantUserDto {
  tenantId: string;
  userName: string;
  emailAddress: string;
  password: string;
  name?: string;
  surname?: string;
  isActive?: boolean;
  roleNames?: string[];
}
