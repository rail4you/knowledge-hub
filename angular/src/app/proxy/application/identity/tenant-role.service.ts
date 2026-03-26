import type { CreateTenantRoleDto, GetTenantRolesInput, TenantRoleDto, UpdateTenantRoleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TenantRoleService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateTenantRoleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantRoleDto>({
      method: 'POST',
      url: '/api/app/tenant-role',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/tenant-role/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantRoleDto>({
      method: 'GET',
      url: `/api/app/tenant-role/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetTenantRolesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TenantRoleDto>>({
      method: 'GET',
      url: '/api/app/tenant-role',
      params: { tenantId: input.tenantId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateTenantRoleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantRoleDto>({
      method: 'PUT',
      url: `/api/app/tenant-role/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}