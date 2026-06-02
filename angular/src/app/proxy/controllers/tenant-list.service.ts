import type { TenantInfoDto, TenantStatsDto, TenantWithStatsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TenantListService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getTenantStatsByTenantId = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantStatsDto>({
      method: 'GET',
      url: `/api/public/tenant-stats/${tenantId}`,
    },
    { apiName: this.apiName,...config });
  

  getTenants = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantInfoDto[]>({
      method: 'GET',
      url: '/api/public/tenants',
    },
    { apiName: this.apiName,...config });
  

  getTenantsWithStats = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantWithStatsDto[]>({
      method: 'GET',
      url: '/api/public/tenants-with-stats',
    },
    { apiName: this.apiName,...config });
}