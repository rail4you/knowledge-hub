import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { TenantUserService } from './proxy/application/identity/tenant-user.service';
import type { IdentityUserCreateDto, IdentityUserDto } from '@abp/ng.identity/proxy';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class CustomIdentityUserService {
  private tenantUserService = inject(TenantUserService);
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: IdentityUserCreateDto): Observable<any> => {
    const tenantId = input.extraProperties?.['tenantId'] as string | undefined;
    
    if (tenantId) {
      const tenantInput = {
        tenantId,
        userName: input.userName || '',
        emailAddress: input.email || '',
        password: input.password || '',
        name: input.name,
        surname: input.surname,
        isActive: input.isActive ?? true,
        roleNames: input.roleNames,
      };
      
      return this.tenantUserService.createUserForTenant(tenantInput);
    }
    
    return this.restService.request<any, any>({
      method: 'POST',
      url: '/api/identity/users',
      body: input,
    }, { apiName: this.apiName });
  };

  delete = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/tenant-user/${id}`,
    }, { apiName: this.apiName });

  findByEmail = (email: string) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'GET',
      url: '/api/identity/users/by-email/' + email,
    }, { apiName: this.apiName });

  findByUsername = (userName: string) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'GET',
      url: '/api/identity/users/by-username/' + userName,
    }, { apiName: this.apiName });

  get = (id: string) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'GET',
      url: `/api/app/tenant-user/${id}`,
    }, { apiName: this.apiName });

  getAssignableRoles = () =>
    this.restService.request<any, any>({
      method: 'GET',
      url: '/api/identity/users/assignable-roles',
    }, { apiName: this.apiName });

  getList = (input: any) =>
    this.restService.request<any, any>({
      method: 'GET',
      url: '/api/app/tenant-user',
      params: input,
    }, { apiName: this.apiName });

  getRoles = (id: string) =>
    this.restService.request<any, any>({
      method: 'GET',
      url: `/api/identity/users/${id}/roles`,
    }, { apiName: this.apiName });

  update = (id: string, input: any) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'PUT',
      url: `/api/app/tenant-user/${id}`,
      body: input,
    }, { apiName: this.apiName });

  updateRoles = (id: string, input: any) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/identity/users/${id}/roles`,
      body: input,
    }, { apiName: this.apiName });
}
