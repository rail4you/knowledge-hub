import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { RolePermissionSummaryDto, UserImportResultDto } from '../users/models';

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
      url: '/api/app/user-import',
      body: excelFile,
    },
    { apiName: this.apiName,...config });
}