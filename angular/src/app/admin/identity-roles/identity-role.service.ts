import type { IdentityRoleDto, GetIdentityRolesInput, IdentityRoleCreateDto, IdentityRoleUpdateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IdentityRoleService {
  private restService = inject(RestService);
  apiName = 'AbpIdentity';

  getAllList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityRoleDto[]>({
      method: 'GET',
      url: '/api/identity/roles/all',
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetIdentityRolesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IdentityRoleDto>>({
      method: 'GET',
      url: '/api/identity/roles',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityRoleDto>({
      method: 'GET',
      url: `/api/identity/roles/${id}`,
    },
    { apiName: this.apiName, ...config });

  create = (input: IdentityRoleCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityRoleDto>({
      method: 'POST',
      url: '/api/identity/roles',
      body: input,
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: IdentityRoleUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityRoleDto>({
      method: 'PUT',
      url: `/api/identity/roles/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/identity/roles/${id}`,
    },
    { apiName: this.apiName, ...config });
}
