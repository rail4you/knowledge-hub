import { Injectable, inject } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';

export interface PermissionItem {
  name: string;
  displayName: string;
  providerName: string;
  providerKey: string;
  isGranted: boolean;
}

export interface SetPermissionForTenantInput {
  tenantId: string | null;
  permissions: PermissionItem[];
}

export interface GetPermissionForTenantParams {
  tenantId: string | null;
  providerName: string;
  providerKey: string;
}

@Injectable({
  providedIn: 'root',
})
export class TenantPermissionService {
  private readonly restService = inject(RestService);
  apiName = 'KnowledgeHub';

  getForTenant(params: GetPermissionForTenantParams) {
    return this.restService.request<any, any[]>({
      method: 'GET',
      url: '/api/knowledge-hub/permissions/get-for-tenant',
      params: {
        tenantId: params.tenantId || '',
        providerName: params.providerName,
        providerKey: params.providerKey,
      }
    }, { apiName: this.apiName });
  }

  setForTenant(input: SetPermissionForTenantInput) {
    return this.restService.request<SetPermissionForTenantInput, void>({
      method: 'POST',
      url: '/api/knowledge-hub/permissions/set-for-tenant',
      body: input,
    }, { apiName: this.apiName });
  }
}
