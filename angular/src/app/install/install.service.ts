import type { InstallStatusDto, InstallInputDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class InstallService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';

  getStatus = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, InstallStatusDto>({
      method: 'GET',
      url: '/api/app/install/status',
    },
    { apiName: this.apiName, ...config });

  install = (input: InstallInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/install/install',
      body: input,
    },
    { apiName: this.apiName, ...config });
}