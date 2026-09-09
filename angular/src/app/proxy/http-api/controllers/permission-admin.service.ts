import type { AssignRoleRequestDto, AssignRoleResultDto, ReseedRequestDto, ReseedResultDto, TenantInfoDto, UserPermissionDiagnosticDto, WhoAmIDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PermissionAdminService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  assignRole = (input: AssignRoleRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssignRoleResultDto>({
      method: 'POST',
      url: '/api/knowledge-hub/admin/permissions/assign-role',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  diagnose = (tenantId: string, userName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserPermissionDiagnosticDto>({
      method: 'GET',
      url: '/api/knowledge-hub/admin/permissions/diagnose',
      params: { tenantId, userName },
    },
    { apiName: this.apiName,...config });
  

  listTenants = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantInfoDto[]>({
      method: 'GET',
      url: '/api/knowledge-hub/admin/permissions/tenants',
    },
    { apiName: this.apiName,...config });
  

  ping = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, object>({
      method: 'GET',
      url: '/api/knowledge-hub/admin/permissions/ping',
    },
    { apiName: this.apiName,...config });
  

  reseedAll = (input: ReseedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReseedResultDto>({
      method: 'POST',
      url: '/api/knowledge-hub/admin/permissions/reseed',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  whoAmI = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, WhoAmIDto>({
      method: 'GET',
      url: '/api/knowledge-hub/admin/permissions/whoami',
    },
    { apiName: this.apiName,...config });
}