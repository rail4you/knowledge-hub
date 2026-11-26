import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ChangePasswordInput, ProfileDto, UpdateProfileDto } from '../volo/abp/account/models';

@Injectable({
  providedIn: 'root',
})
export class UnifiedProfileService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  changePassword = (input: ChangePasswordInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/unified-profile/change-password',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProfileDto>({
      method: 'GET',
      url: '/api/app/unified-profile',
    },
    { apiName: this.apiName,...config });
  

  update = (input: UpdateProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProfileDto>({
      method: 'PUT',
      url: '/api/app/unified-profile',
      body: input,
    },
    { apiName: this.apiName,...config });
}