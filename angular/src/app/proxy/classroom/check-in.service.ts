import type { CheckInRosterDto, CheckInSessionDto, CreateCheckInSessionInput, DoCheckInInput, DoCheckInResultDto, MyCheckInRecordDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CheckInService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CheckInSessionDto>({
      method: 'POST',
      url: `/api/app/check-in/${id}/close`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateCheckInSessionInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CheckInSessionDto>({
      method: 'POST',
      url: '/api/app/check-in',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/check-in/${id}`,
    },
    { apiName: this.apiName,...config });
  

  doCheckIn = (input: DoCheckInInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoCheckInResultDto>({
      method: 'POST',
      url: '/api/app/check-in/do-check-in',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CheckInSessionDto>({
      method: 'GET',
      url: `/api/app/check-in/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (courseId: string, skipCount: number, maxResultCount: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CheckInSessionDto>>({
      method: 'GET',
      url: '/api/app/check-in',
      params: { courseId, skipCount, maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyRecords = (courseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MyCheckInRecordDto[]>({
      method: 'GET',
      url: '/api/app/check-in/my-records',
      params: { courseId },
    },
    { apiName: this.apiName,...config });
  

  getRoster = (sessionId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CheckInRosterDto>({
      method: 'GET',
      url: `/api/app/check-in/roster/${sessionId}`,
    },
    { apiName: this.apiName,...config });
}