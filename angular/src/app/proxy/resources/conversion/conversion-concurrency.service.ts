import type { ConversionConcurrencyDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ConversionConcurrencyService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ConversionConcurrencyDto[]>({
      method: 'GET',
      url: '/api/app/conversion-concurrency',
    },
    { apiName: this.apiName,...config });
  

  setMaxConcurrent = (serviceName: string, maxConcurrent: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ConversionConcurrencyDto>({
      method: 'POST',
      url: '/api/app/conversion-concurrency/set-max-concurrent',
      params: { serviceName, maxConcurrent },
    },
    { apiName: this.apiName,...config });
}