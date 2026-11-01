import type { AdvanceProjectStageDto, CompleteMilestoneDto, CooperationDemandDto, CooperationDemandListRequestDto, CooperationProjectDto, CooperationResponseDto, CreateCooperationProjectDto, CreateCooperationResponseDto, CreateMilestoneDto, CreateUpdateCooperationDemandDto, DecideCooperationResponseDto, ProjectMilestoneDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto, PagedResultRequestDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CooperationService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  addMilestone = (input: CreateMilestoneDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectMilestoneDto>({
      method: 'POST',
      url: '/api/app/cooperation/milestone',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  advanceStage = (input: AdvanceProjectStageDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CooperationProjectDto>({
      method: 'POST',
      url: '/api/app/cooperation/advance-stage',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  closeDemand = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CooperationDemandDto>({
      method: 'POST',
      url: `/api/app/cooperation/${id}/close-demand`,
    },
    { apiName: this.apiName,...config });
  

  completeMilestone = (input: CompleteMilestoneDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectMilestoneDto>({
      method: 'POST',
      url: '/api/app/cooperation/complete-milestone',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createDemand = (input: CreateUpdateCooperationDemandDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CooperationDemandDto>({
      method: 'POST',
      url: '/api/app/cooperation/demand',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createProject = (input: CreateCooperationProjectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CooperationProjectDto>({
      method: 'POST',
      url: '/api/app/cooperation/project',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  decideResponse = (responseId: string, input: DecideCooperationResponseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CooperationResponseDto>({
      method: 'POST',
      url: `/api/app/cooperation/decide-response/${responseId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getDemand = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CooperationDemandDto>({
      method: 'GET',
      url: `/api/app/cooperation/${id}/demand`,
    },
    { apiName: this.apiName,...config });
  

  getHall = (input: CooperationDemandListRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CooperationDemandDto>>({
      method: 'GET',
      url: '/api/app/cooperation/hall',
      params: { type: input.type, status: input.status, region: input.region, major: input.major, filter: input.filter, onlyMine: input.onlyMine, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyDemands = (input: CooperationDemandListRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CooperationDemandDto>>({
      method: 'GET',
      url: '/api/app/cooperation/my-demands',
      params: { type: input.type, status: input.status, region: input.region, major: input.major, filter: input.filter, onlyMine: input.onlyMine, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getProjects = (input: PagedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CooperationProjectDto>>({
      method: 'GET',
      url: '/api/app/cooperation/projects',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getResponses = (demandId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CooperationResponseDto[]>({
      method: 'GET',
      url: `/api/app/cooperation/responses/${demandId}`,
    },
    { apiName: this.apiName,...config });
  

  respond = (input: CreateCooperationResponseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CooperationResponseDto>({
      method: 'POST',
      url: '/api/app/cooperation/respond',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submitDemand = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CooperationDemandDto>({
      method: 'POST',
      url: `/api/app/cooperation/${id}/submit-demand`,
    },
    { apiName: this.apiName,...config });
  

  updateDemand = (id: string, input: CreateUpdateCooperationDemandDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CooperationDemandDto>({
      method: 'PUT',
      url: `/api/app/cooperation/${id}/demand`,
      body: input,
    },
    { apiName: this.apiName,...config });
}