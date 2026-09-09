import type { AccountValidityDto, GetAccountValidityListInput, SetAccountValidityBatchInput, SetAccountValidityInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AccountValidityService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountValidityDto>({
      method: 'GET',
      url: `/api/app/account-validity/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAccountValidityListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AccountValidityDto>>({
      method: 'GET',
      url: '/api/app/account-validity',
      params: { filter: input.filter, tenantId: input.tenantId, roleName: input.roleName, status: input.status, expiringSoon: input.expiringSoon, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  set = (input: SetAccountValidityInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountValidityDto>({
      method: 'POST',
      url: '/api/app/account-validity/set',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  setBatch = (input: SetAccountValidityBatchInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/account-validity/set-batch',
      body: input,
    },
    { apiName: this.apiName,...config });
}