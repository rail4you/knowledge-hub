import type { MaterialBriefDto, PortalHomeDataDto, PublicBrowseDto, PublicHomeStatsDto, TenantResourceSummaryDto } from './models';
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
  

  getPublicBrowse = (tenantId: string, majorId: string, search: string, skipCount: number, maxResultCount: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PublicBrowseDto>({
      method: 'GET',
      url: '/api/app/portal/public-browse',
      params: { tenantId, majorId, search, skipCount, maxResultCount },
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
  

  getTopResourcesByDownload = (count: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialBriefDto[]>({
      method: 'GET',
      url: '/api/app/portal/top-resources-by-download',
      params: { count },
    },
    { apiName: this.apiName,...config });
}