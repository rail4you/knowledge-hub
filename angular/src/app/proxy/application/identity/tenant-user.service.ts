import { Injectable, inject } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { IdentityUserDto } from '@abp/ng.identity/proxy';
import type { CreateTenantUserDto } from './models';

@Injectable({
  providedIn: 'root',
})
export class TenantUserService {
  private readonly restService = inject(RestService);
  apiName = 'Default';

  createUserForTenant = (input: CreateTenantUserDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'POST',
      url: '/api/app/tenant-user/user-for-tenant',
      body: input,
    }, { apiName: this.apiName, ...config });
}
