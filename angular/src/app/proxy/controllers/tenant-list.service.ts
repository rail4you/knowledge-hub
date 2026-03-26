import type { TenantInfoDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TenantListService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getTenants = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantInfoDto[]>({
      method: 'GET',
      url: '/api/public/tenants',
    },
    { apiName: this.apiName,...config });
}