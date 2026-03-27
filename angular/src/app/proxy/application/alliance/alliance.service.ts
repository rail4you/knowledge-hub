import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto, PagedResultRequestDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AllianceMemberRole } from '../../alliance/enums/alliance-member-role.enum';
import type { AllianceAuditDto, AllianceAuditInputDto, AllianceDto, AllianceMemberDto, AllianceMemberQueryDto, CreateAllianceDto, CreateAllianceMemberDto, PendingAllianceAuditDto } from '../contracts/alliance/models';

@Injectable({
  providedIn: 'root',
})
export class AllianceService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  addMember = (input: CreateAllianceMemberDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AllianceMemberDto>({
      method: 'POST',
      url: '/api/app/alliance/member',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateAllianceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AllianceDto>({
      method: 'POST',
      url: '/api/app/alliance',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/alliance/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AllianceDto>({
      method: 'GET',
      url: `/api/app/alliance/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AllianceDto>>({
      method: 'GET',
      url: '/api/app/alliance',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMembers = (input: AllianceMemberQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AllianceMemberDto>>({
      method: 'GET',
      url: '/api/app/alliance/members',
      params: { allianceId: input.allianceId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPendingAudits = (allianceId: string, input: PagedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PendingAllianceAuditDto>>({
      method: 'GET',
      url: `/api/app/alliance/pending-audits/${allianceId}`,
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getResourceAudits = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AllianceAuditDto[]>({
      method: 'GET',
      url: `/api/app/alliance/resource-audits/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getTenantAllianceMembership = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AllianceMemberDto>({
      method: 'GET',
      url: `/api/app/alliance/tenant-alliance-membership/${tenantId}`,
    },
    { apiName: this.apiName,...config });
  

  isAllianceEnabled = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: '/api/app/alliance/is-alliance-enabled',
    },
    { apiName: this.apiName,...config });
  

  isTenantInAlliance = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: `/api/app/alliance/is-tenant-in-alliance/${tenantId}`,
    },
    { apiName: this.apiName,...config });
  

  leagueAudit = (input: AllianceAuditInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AllianceAuditDto>({
      method: 'POST',
      url: '/api/app/alliance/league-audit',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  removeMember = (memberId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/alliance/member/${memberId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateAllianceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AllianceDto>({
      method: 'PUT',
      url: `/api/app/alliance/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateMemberRole = (memberId: string, role: AllianceMemberRole, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AllianceMemberDto>({
      method: 'PUT',
      url: `/api/app/alliance/member-role/${memberId}`,
      params: { role },
    },
    { apiName: this.apiName,...config });
}