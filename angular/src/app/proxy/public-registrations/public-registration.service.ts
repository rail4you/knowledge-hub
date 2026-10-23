import type { PublicRegistrationDto, PublicRegistrationResultDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { PublicSchoolDto } from '../schools/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class PublicRegistrationService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getSocialSchool = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PublicSchoolDto>({
      method: 'GET',
      url: '/api/app/public-registration/social-school',
    },
    { apiName: this.apiName,...config });
  

  register = (input: PublicRegistrationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PublicRegistrationResultDto>({
      method: 'POST',
      url: '/api/app/public-registration/register',
      body: input,
    },
    { apiName: this.apiName,...config });
}