import type { ProductFeatureDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProductFeatureService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductFeatureDto>({
      method: 'GET',
      url: '/api/app/product-feature',
    },
    { apiName: this.apiName,...config });
}