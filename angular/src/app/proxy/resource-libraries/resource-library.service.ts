import type { AddResourceLibraryMemberDto, AssignResourceLibraryTeacherDto, CreateResourceLibraryDto, ResourceLibraryDto, ResourceLibraryListRequestDto, ResourceLibraryMemberDto, ResourceLibraryTeacherDto, ResourceLibraryTeacherListRequestDto, UpdateResourceLibraryDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ResourceLibraryService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  addMember = (id: string, input: AddResourceLibraryMemberDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryMemberDto>({
      method: 'POST',
      url: `/api/app/resource-library/${id}/member`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  assignTeacher = (id: string, input: AssignResourceLibraryTeacherDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryTeacherDto>({
      method: 'POST',
      url: `/api/app/resource-library/${id}/assign-teacher`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateResourceLibraryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryDto>({
      method: 'POST',
      url: '/api/app/resource-library',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource-library/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryDto>({
      method: 'GET',
      url: `/api/app/resource-library/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAssignableTeachers = (input: ResourceLibraryTeacherListRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryTeacherDto[]>({
      method: 'GET',
      url: '/api/app/resource-library/assignable-teachers',
      params: { filter: input.filter, libraryId: input.libraryId },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: ResourceLibraryListRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryDto[]>({
      method: 'GET',
      url: '/api/app/resource-library',
      params: { primaryMajorId: input.primaryMajorId, onlyJoined: input.onlyJoined, visibility: input.visibility },
    },
    { apiName: this.apiName,...config });
  

  getTeachers = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryTeacherDto[]>({
      method: 'GET',
      url: `/api/app/resource-library/${id}/teachers`,
    },
    { apiName: this.apiName,...config });
  

  removeMember = (id: string, memberTenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource-library/${id}/member/${memberTenantId}`,
    },
    { apiName: this.apiName,...config });
  

  removeTeacher = (id: string, userId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource-library/${id}/teacher/${userId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateResourceLibraryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryDto>({
      method: 'PUT',
      url: `/api/app/resource-library/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}