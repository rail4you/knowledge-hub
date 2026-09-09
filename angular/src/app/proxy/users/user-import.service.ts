import type { RolePermissionSummaryDto, UserImportResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UserImportService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getRolePermissionSummary = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, RolePermissionSummaryDto[]>({
      method: 'GET',
      url: '/api/app/user-import/role-permission-summary',
    },
    { apiName: this.apiName,...config });
  

  import = (excelFile: number[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserImportResultDto>({
      method: 'POST',
      url: '/api/app/user-import/import',
      body: excelFile,
    },
    { apiName: this.apiName,...config });
}