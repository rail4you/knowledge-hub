import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class HttpProxyService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  proxyGet = (host: string, path: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/proxy/http/${host}/${path}`,
    },
    { apiName: this.apiName,...config });
  

  proxyPost = (host: string, path: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/proxy/http/${host}/${path}`,
    },
    { apiName: this.apiName,...config });
}