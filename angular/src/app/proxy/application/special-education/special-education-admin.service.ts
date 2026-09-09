import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { SeedMockDataInputDto, SetSpecialEduTenantEnabledDto, SpecialEduTenantStateDto } from '../../special-education/dtos/models';
import type { SeedMockDataResultDto } from '../../special-education/models';

@Injectable({
  providedIn: 'root',
})
export class SpecialEducationAdminService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getTenantStates = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduTenantStateDto[]>({
      method: 'GET',
      url: '/api/app/special-education-admin/tenant-states',
    },
    { apiName: this.apiName,...config });
  

  seedMockData = (input: SeedMockDataInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SeedMockDataResultDto>({
      method: 'POST',
      url: '/api/app/special-education-admin/seed-mock-data',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  setTenantEnabled = (input: SetSpecialEduTenantEnabledDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/special-education-admin/set-tenant-enabled',
      body: input,
    },
    { apiName: this.apiName,...config });
}