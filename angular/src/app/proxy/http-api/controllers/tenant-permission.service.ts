import type { SetPermissionForTenantInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TenantPermissionService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  setForTenant = (input: SetPermissionForTenantInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/knowledge-hub/permissions/set-for-tenant',
      body: input,
    },
    { apiName: this.apiName,...config });
}