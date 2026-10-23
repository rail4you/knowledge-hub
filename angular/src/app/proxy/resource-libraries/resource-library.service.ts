import type { AddResourceLibraryMemberDto, CreateResourceLibraryDto, ResourceLibraryDto, ResourceLibraryListRequestDto, ResourceLibraryMemberDto, UpdateResourceLibraryDto } from './dtos/models';
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
  

  getList = (input: ResourceLibraryListRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryDto[]>({
      method: 'GET',
      url: '/api/app/resource-library',
      params: { primaryMajorId: input.primaryMajorId, onlyJoined: input.onlyJoined },
    },
    { apiName: this.apiName,...config });
  

  removeMember = (id: string, memberTenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource-library/${id}/member/${memberTenantId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateResourceLibraryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryDto>({
      method: 'PUT',
      url: `/api/app/resource-library/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  validateAttach = (libraryId: string, majorId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/resource-library/validate-attach',
      params: { libraryId, majorId },
    },
    { apiName: this.apiName,...config });
}