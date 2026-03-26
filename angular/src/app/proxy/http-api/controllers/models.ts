
export interface PermissionItem {
  name?: string;
  providerName?: string;
  providerKey?: string;
  isGranted?: boolean;
}

export interface SetPermissionForTenantInput {
  tenantId?: string | null;
  permissions?: PermissionItem[];
}
