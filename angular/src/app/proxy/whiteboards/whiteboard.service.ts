import type { CreateWhiteboardDto, InvitableTeacherDto, InviteWhiteboardMembersDto, UpdateWhiteboardDto, WhiteboardDetailDto, WhiteboardListItemDto, WhiteboardMemberDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WhiteboardService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  clearElements = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/whiteboard/${id}/clear-elements`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateWhiteboardDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WhiteboardDetailDto>({
      method: 'POST',
      url: '/api/app/whiteboard',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/whiteboard/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WhiteboardDetailDto>({
      method: 'GET',
      url: `/api/app/whiteboard/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getInvitableTeachers = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvitableTeacherDto[]>({
      method: 'GET',
      url: `/api/app/whiteboard/${id}/invitable-teachers`,
    },
    { apiName: this.apiName,...config });
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, WhiteboardListItemDto[]>({
      method: 'GET',
      url: '/api/app/whiteboard',
    },
    { apiName: this.apiName,...config });
  

  invite = (id: string, input: InviteWhiteboardMembersDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WhiteboardMemberDto[]>({
      method: 'POST',
      url: `/api/app/whiteboard/${id}/invite`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  removeMember = (id: string, userId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/whiteboard/${id}/member/${userId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateWhiteboardDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WhiteboardDetailDto>({
      method: 'PUT',
      url: `/api/app/whiteboard/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}