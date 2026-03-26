import type { CreateTenantUserDto, UpdateTenantUserDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { GetIdentityUsersInput, IdentityUserDto } from '../../volo/abp/identity/models';

@Injectable({
  providedIn: 'root',
})
export class TenantUserService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  createUserForTenant = (input: CreateTenantUserDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'POST',
      url: '/api/app/tenant-user/user-for-tenant',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/tenant-user/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'GET',
      url: `/api/app/tenant-user/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetIdentityUsersInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IdentityUserDto>>({
      method: 'GET',
      url: '/api/app/tenant-user',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount, extraProperties: input.extraProperties },
    },
    { apiName: this.apiName,...config });
  

  getRolesForUser = (userId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'GET',
      url: `/api/app/tenant-user/roles-for-user/${userId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateTenantUserDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'PUT',
      url: `/api/app/tenant-user/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}