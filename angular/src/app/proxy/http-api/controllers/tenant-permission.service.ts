import type { SetPermissionForTenantInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TenantPermissionService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getForTenant = (tenantId: string, providerName: string, providerKey: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, object>({
      method: 'GET',
      url: '/api/knowledge-hub/permissions/get-for-tenant',
      params: { tenantId, providerName, providerKey },
    },
    { apiName: this.apiName,...config });
  

  setForTenant = (input: SetPermissionForTenantInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/knowledge-hub/permissions/set-for-tenant',
      body: input,
    },
    { apiName: this.apiName,...config });
}