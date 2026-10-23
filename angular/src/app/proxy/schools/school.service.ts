import type { PublicSchoolDto, SchoolDto, SchoolProfileDto, UpdateSchoolDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SchoolService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getCurrent = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SchoolDto>({
      method: 'GET',
      url: '/api/app/school/current',
    },
    { apiName: this.apiName,...config });
  

  getProfile = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SchoolProfileDto>({
      method: 'GET',
      url: `/api/app/school/profile/${tenantId}`,
    },
    { apiName: this.apiName,...config });
  

  getPublicList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SchoolDto[]>({
      method: 'GET',
      url: '/api/app/school/public-list',
    },
    { apiName: this.apiName,...config });
  

  getPublicSchool = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PublicSchoolDto>({
      method: 'GET',
      url: '/api/app/school/public-school',
    },
    { apiName: this.apiName,...config });
  

  setPublicSchool = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PublicSchoolDto>({
      method: 'POST',
      url: `/api/app/school/set-public-school/${tenantId}`,
    },
    { apiName: this.apiName,...config });
  

  updateCurrent = (input: UpdateSchoolDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SchoolDto>({
      method: 'PUT',
      url: '/api/app/school/current',
      body: input,
    },
    { apiName: this.apiName,...config });
}