import type { AssignGroupsInput, CreateGroupTaskInput, GradeGroupTaskInput, GroupTaskDetailDto, GroupTaskDto, GroupTaskSubmissionDto, SubmitGroupTaskInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class GroupTaskService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  assignGroups = (input: AssignGroupsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskDetailDto>({
      method: 'POST',
      url: '/api/app/group-task/assign-groups',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  autoAssignGroups = (id: string, groupCount: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskDetailDto>({
      method: 'POST',
      url: `/api/app/group-task/${id}/auto-assign-groups`,
      params: { groupCount },
    },
    { apiName: this.apiName,...config });
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskDto>({
      method: 'POST',
      url: `/api/app/group-task/${id}/close`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateGroupTaskInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskDto>({
      method: 'POST',
      url: '/api/app/group-task',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/group-task/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getDetail = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskDetailDto>({
      method: 'GET',
      url: `/api/app/group-task/${id}/detail`,
    },
    { apiName: this.apiName,...config });
  

  getList = (courseId: string, skipCount: number, maxResultCount: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<GroupTaskDto>>({
      method: 'GET',
      url: '/api/app/group-task',
      params: { courseId, skipCount, maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyTask = (token: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskDetailDto>({
      method: 'GET',
      url: '/api/app/group-task/my-task',
      params: { token },
    },
    { apiName: this.apiName,...config });
  

  getMyTasks = (courseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskDto[]>({
      method: 'GET',
      url: '/api/app/group-task/my-tasks',
      params: { courseId },
    },
    { apiName: this.apiName,...config });
  

  grade = (input: GradeGroupTaskInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskSubmissionDto>({
      method: 'POST',
      url: '/api/app/group-task/grade',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  publish = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskDto>({
      method: 'POST',
      url: `/api/app/group-task/${id}/publish`,
    },
    { apiName: this.apiName,...config });
  

  submit = (input: SubmitGroupTaskInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskSubmissionDto>({
      method: 'POST',
      url: '/api/app/group-task/submit',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateGroupTaskInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GroupTaskDto>({
      method: 'PUT',
      url: `/api/app/group-task/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}