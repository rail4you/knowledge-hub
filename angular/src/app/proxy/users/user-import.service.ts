import type { ImportUsersFileDto, RolePermissionSummaryDto, UserImportResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UserImportService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getImportTemplate = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      url: '/api/app/user-import/import-template',
    },
    { apiName: this.apiName,...config });
  

  getRolePermissionSummary = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, RolePermissionSummaryDto[]>({
      method: 'GET',
      url: '/api/app/user-import/role-permission-summary',
    },
    { apiName: this.apiName,...config });
  

  import = (input: ImportUsersFileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserImportResultDto>({
      method: 'POST',
      url: '/api/app/user-import/import',
      body: input,
    },
    { apiName: this.apiName,...config });
}