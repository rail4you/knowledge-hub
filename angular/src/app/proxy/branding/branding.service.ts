import type { BrandingDto, UpdateBrandingDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BrandingService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, BrandingDto>({
      method: 'GET',
      url: '/api/app/branding',
    },
    { apiName: this.apiName,...config });
  

  update = (input: UpdateBrandingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BrandingDto>({
      method: 'PUT',
      url: '/api/app/branding',
      body: input,
    },
    { apiName: this.apiName,...config });
}