import type { PortalHomeDataDto, PublicHomeStatsDto, TenantResourceSummaryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PortalService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getHomeData = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PortalHomeDataDto>({
      method: 'GET',
      url: `/api/app/portal/home-data/${tenantId}`,
    },
    { apiName: this.apiName,...config });
  

  getPublicHomeStats = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PublicHomeStatsDto>({
      method: 'GET',
      url: '/api/app/portal/public-home-stats',
    },
    { apiName: this.apiName,...config });
  

  getPublicTenantList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantResourceSummaryDto[]>({
      method: 'GET',
      url: '/api/app/portal/public-tenant-list',
    },
    { apiName: this.apiName,...config });
}