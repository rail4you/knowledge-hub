import type { CurrentTenantDto, LoginAttemptInfoDto, TenantInfoDto, TenantStatsDto, TenantWithStatsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TenantListService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getCurrentTenant = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CurrentTenantDto>({
      method: 'GET',
      url: '/api/public/current-tenant',
    },
    { apiName: this.apiName,...config });
  

  getLoginAttemptInfoByUserName = (userName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoginAttemptInfoDto>({
      method: 'GET',
      url: '/api/public/login-attempt-info',
      params: { userName },
    },
    { apiName: this.apiName,...config });
  

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