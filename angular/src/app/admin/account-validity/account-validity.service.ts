import { inject, Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Observable } from 'rxjs';

export interface AccountValidityItem {
  id: string;
  userId: string;
  tenantId?: string | null;
  tenantName?: string | null;
  userName: string;
  displayName?: string | null;
  roleName: string;
  validUntil?: string | null;
  /** 0=正常 1=已到期；undefined=从未设置（永久有效） */
  status?: number | null;
  isExpired: boolean;
  isExpiringSoon: boolean;
  remainingDays?: number | null;
  userIsActive: boolean;
  lastModifiedTime?: string | null;
}

export interface AccountValidityListParams {
  filter?: string;
  tenantId?: string;
  roleName?: string;
  status?: number;
  expiringSoon?: boolean;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export class AccountValidityService {
  private readonly rest = inject(RestService);

  getList(params: AccountValidityListParams): Observable<PagedResultDto<AccountValidityItem>> {
    return this.rest.request<any, PagedResultDto<AccountValidityItem>>({
      method: 'GET',
      url: '/api/app/account-validity',
      params,
    });
  }

  setValidity(body: { userId: string; validUntil?: string | null; permanent: boolean; days?: number | null }): Observable<AccountValidityItem> {
    return this.rest.request<any, AccountValidityItem>({
      method: 'POST',
      url: '/api/app/account-validity/set',
      body,
    });
  }

  setBatch(body: { userIds: string[]; validUntil?: string | null; permanent: boolean; days?: number | null }): Observable<void> {
    return this.rest.request<any, void>({
      method: 'POST',
      url: '/api/app/account-validity/set-batch',
      body,
    });
  }
}
