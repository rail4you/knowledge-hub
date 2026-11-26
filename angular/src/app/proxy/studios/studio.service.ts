import type { CreateStudioDto, CreateStudioMaterialDto, ReviewStudioMaterialDto, StudioDetailDto, StudioListItemDto, StudioMaterialDto, StudioMemberDto, StudioMemberInputDto, UpdateStudioDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StudioService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  addMember = (id: string, input: StudioMemberInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudioMemberDto[]>({
      method: 'POST',
      url: `/api/app/studio/${id}/member`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateStudioDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudioDetailDto>({
      method: 'POST',
      url: '/api/app/studio',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createMaterial = (id: string, input: CreateStudioMaterialDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudioMaterialDto>({
      method: 'POST',
      url: `/api/app/studio/${id}/material`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/studio/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteMaterial = (id: string, materialId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/studio/${id}/material/${materialId}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudioDetailDto>({
      method: 'GET',
      url: `/api/app/studio/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getCandidateTeachers = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudioMemberDto[]>({
      method: 'GET',
      url: `/api/app/studio/${id}/candidate-teachers`,
    },
    { apiName: this.apiName,...config });
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudioListItemDto[]>({
      method: 'GET',
      url: '/api/app/studio',
    },
    { apiName: this.apiName,...config });
  

  removeMember = (id: string, userId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/studio/${id}/member/${userId}`,
    },
    { apiName: this.apiName,...config });
  

  reviewMaterial = (id: string, materialId: string, input: ReviewStudioMaterialDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudioMaterialDto>({
      method: 'POST',
      url: `/api/app/studio/${id}/review-material/${materialId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submitMaterial = (id: string, materialId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudioMaterialDto>({
      method: 'POST',
      url: `/api/app/studio/${id}/submit-material/${materialId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateStudioDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudioDetailDto>({
      method: 'PUT',
      url: `/api/app/studio/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}