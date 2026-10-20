import type { OssConfigDto, OssTestResultDto, UpdateOssConfigDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class OssConfigService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, OssConfigDto>({
      method: 'GET',
      url: '/api/app/oss-config',
    },
    { apiName: this.apiName,...config });
  

  test = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, OssTestResultDto>({
      method: 'POST',
      url: '/api/app/oss-config/test',
    },
    { apiName: this.apiName,...config });
  

  update = (input: UpdateOssConfigDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OssConfigDto>({
      method: 'PUT',
      url: '/api/app/oss-config',
      body: input,
    },
    { apiName: this.apiName,...config });
}