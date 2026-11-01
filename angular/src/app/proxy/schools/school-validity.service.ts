import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { SchoolValidityDto, SetSchoolValidityDto } from '../enterprises/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class SchoolValidityService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SchoolValidityDto[]>({
      method: 'GET',
      url: '/api/app/school-validity',
    },
    { apiName: this.apiName,...config });
  

  setValidity = (input: SetSchoolValidityDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SchoolValidityDto>({
      method: 'POST',
      url: '/api/app/school-validity/set-validity',
      body: input,
    },
    { apiName: this.apiName,...config });
}