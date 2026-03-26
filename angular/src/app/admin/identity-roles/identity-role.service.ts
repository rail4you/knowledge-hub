import type { IdentityRoleDto, GetIdentityRolesInput, IdentityRoleCreateDto, IdentityRoleUpdateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IdentityRoleService {
  private restService = inject(RestService);
  apiName = 'Default';

  getAllList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityRoleDto[]>({
      method: 'GET',
      url: '/api/app/tenant-role/all',
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetIdentityRolesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IdentityRoleDto>>({
      method: 'GET',
      url: '/api/app/tenant-role',
      params: { 
        filter: input.filter, 
        sorting: input.sorting, 
        skipCount: input.skipCount, 
        maxResultCount: input.maxResultCount,
        tenantId: input.tenantId
      },
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityRoleDto>({
      method: 'GET',
      url: `/api/app/tenant-role/${id}`,
    },
    { apiName: this.apiName, ...config });

  create = (input: IdentityRoleCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityRoleDto>({
      method: 'POST',
      url: '/api/app/tenant-role',
      body: input,
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: IdentityRoleUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityRoleDto>({
      method: 'PUT',
      url: `/api/app/tenant-role/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/tenant-role/${id}`,
    },
    { apiName: this.apiName, ...config });
}
