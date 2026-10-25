import type { AddTeachingClassMembersDto, CreateTeachingClassDto, GetTeachingClassMembersInput, GetTeachingClassesInput, TeachingClassDto, TeachingClassMemberDto, UpdateTeachingClassDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TeachingClassService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  addMembers = (classId: string, input: AddTeachingClassMembersDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'POST',
      url: `/api/app/teaching-class/members/${classId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateTeachingClassDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingClassDto>({
      method: 'POST',
      url: '/api/app/teaching-class',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/teaching-class/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingClassDto>({
      method: 'GET',
      url: `/api/app/teaching-class/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetTeachingClassesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TeachingClassDto>>({
      method: 'GET',
      url: '/api/app/teaching-class',
      params: { filter: input.filter, majorId: input.majorId, courseId: input.courseId, teacherId: input.teacherId, status: input.status, tenantId: input.tenantId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMembers = (input: GetTeachingClassMembersInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TeachingClassMemberDto>>({
      method: 'GET',
      url: '/api/app/teaching-class/members',
      params: { classId: input.classId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  removeMember = (classId: string, studentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/teaching-class/member',
      params: { classId, studentId },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateTeachingClassDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingClassDto>({
      method: 'PUT',
      url: `/api/app/teaching-class/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}