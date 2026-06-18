import type { CreateUpdateTenantInfoDto, TenantInfoDto, TenantKnowledgeGraphDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TenantInfoService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';

  getCurrent = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantInfoDto>({
      method: 'GET',
      url: '/api/app/tenant-info/current',
    },
    { apiName: this.apiName, ...config });

  getByTenantId = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantInfoDto>({
      method: 'GET',
      url: `/api/app/tenant-info/by-tenant-id/${tenantId}`,
    },
    { apiName: this.apiName, ...config });

  saveCurrent = (input: CreateUpdateTenantInfoDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantInfoDto>({
      method: 'POST',
      url: '/api/app/tenant-info/save-current',
      body: input,
    },
    { apiName: this.apiName, ...config });

  saveByTenantId = (tenantId: string, input: CreateUpdateTenantInfoDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantInfoDto>({
      method: 'POST',
      url: `/api/app/tenant-info/save-by-tenant-id/${tenantId}`,
      body: input,
    },
    { apiName: this.apiName, ...config });

  getKnowledgeGraph = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantKnowledgeGraphDto>({
      method: 'GET',
      url: `/api/app/tenant-info/knowledge-graph/${tenantId}`,
    },
    { apiName: this.apiName, ...config });

  getCurrentKnowledgeGraph = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantKnowledgeGraphDto>({
      method: 'GET',
      url: '/api/app/tenant-info/current-knowledge-graph',
    },
    { apiName: this.apiName, ...config });
}
