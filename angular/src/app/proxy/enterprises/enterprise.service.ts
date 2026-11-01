import type { BindEnterpriseMemberDto, CreateUpdateEnterpriseProfileDto, EnterpriseMemberDto, EnterpriseProfileDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto, PagedResultRequestDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EnterpriseService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  bindMember = (input: BindEnterpriseMemberDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EnterpriseMemberDto>({
      method: 'POST',
      url: '/api/app/enterprise/bind-member',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateEnterpriseProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EnterpriseProfileDto>({
      method: 'POST',
      url: '/api/app/enterprise',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EnterpriseProfileDto>({
      method: 'GET',
      url: `/api/app/enterprise/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<EnterpriseProfileDto>>({
      method: 'GET',
      url: '/api/app/enterprise',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMembers = (enterpriseProfileId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EnterpriseMemberDto[]>({
      method: 'GET',
      url: `/api/app/enterprise/members/${enterpriseProfileId}`,
    },
    { apiName: this.apiName,...config });
  

  getMine = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, EnterpriseProfileDto>({
      method: 'GET',
      url: '/api/app/enterprise/mine',
    },
    { apiName: this.apiName,...config });
  

  submitCertification = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EnterpriseProfileDto>({
      method: 'POST',
      url: `/api/app/enterprise/${id}/submit-certification`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateEnterpriseProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EnterpriseProfileDto>({
      method: 'PUT',
      url: `/api/app/enterprise/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}