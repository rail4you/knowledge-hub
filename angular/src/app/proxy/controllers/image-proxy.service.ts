import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class ImageProxyService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  get = (url: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/image-proxy',
      params: { url },
    },
    { apiName: this.apiName,...config });
}