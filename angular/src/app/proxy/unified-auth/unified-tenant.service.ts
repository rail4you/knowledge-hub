import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { GetTenantsInput, TenantCreateDto, TenantDto, TenantUpdateDto } from '../volo/abp/tenant-management/models';

@Injectable({
  providedIn: 'root',
})
export class UnifiedTenantService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: TenantCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantDto>({
      method: 'POST',
      url: '/api/app/unified-tenant',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/unified-tenant/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteDefaultConnectionString = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/unified-tenant/${id}/default-connection-string`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantDto>({
      method: 'GET',
      url: `/api/app/unified-tenant/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getDefaultConnectionString = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'GET',
      responseType: 'text',
      url: `/api/app/unified-tenant/${id}/default-connection-string`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetTenantsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TenantDto>>({
      method: 'GET',
      url: '/api/app/unified-tenant',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: TenantUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantDto>({
      method: 'PUT',
      url: `/api/app/unified-tenant/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateDefaultConnectionString = (id: string, defaultConnectionString: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/app/unified-tenant/${id}/default-connection-string`,
      params: { defaultConnectionString },
    },
    { apiName: this.apiName,...config });
}