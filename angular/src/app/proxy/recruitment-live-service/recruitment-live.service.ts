import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateRecruitmentLiveDto, IceServerDto, PagedRecruitmentLiveRequestDto, RecruitmentLiveDto, UpdateRecruitmentLiveDto, UserBriefDto, WsTokenDto } from '../recruitment-live/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class RecruitmentLiveService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  cancelLive = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/recruitment-live/${id}/cancel-live`,
    },
    { apiName: this.apiName,...config });
  

  createLive = (input: CreateRecruitmentLiveDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecruitmentLiveDto>({
      method: 'POST',
      url: '/api/app/recruitment-live/live',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deleteLive = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/recruitment-live/${id}/live`,
    },
    { apiName: this.apiName,...config });
  

  getIceServers = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IceServerDto[]>({
      method: 'GET',
      url: '/api/app/recruitment-live/ice-servers',
    },
    { apiName: this.apiName,...config });
  

  getLive = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecruitmentLiveDto>({
      method: 'GET',
      url: `/api/app/recruitment-live/${id}/live`,
    },
    { apiName: this.apiName,...config });
  

  getStudentLives = (input: PagedRecruitmentLiveRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<RecruitmentLiveDto>>({
      method: 'GET',
      url: '/api/app/recruitment-live/student-lives',
      params: { filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTeacherLives = (input: PagedRecruitmentLiveRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<RecruitmentLiveDto>>({
      method: 'GET',
      url: '/api/app/recruitment-live/teacher-lives',
      params: { filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTenantStudents = (filter: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserBriefDto[]>({
      method: 'GET',
      url: '/api/app/recruitment-live/tenant-students',
      params: { filter },
    },
    { apiName: this.apiName,...config });
  

  getWebSocketToken = (liveId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WsTokenDto>({
      method: 'GET',
      url: `/api/app/recruitment-live/web-socket-token/${liveId}`,
    },
    { apiName: this.apiName,...config });
  

  updateLive = (id: string, input: UpdateRecruitmentLiveDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecruitmentLiveDto>({
      method: 'PUT',
      url: `/api/app/recruitment-live/${id}/live`,
      body: input,
    },
    { apiName: this.apiName,...config });
}