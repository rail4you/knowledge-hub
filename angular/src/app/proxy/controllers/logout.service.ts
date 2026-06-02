import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class LogoutService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  clearSession = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/app/logout/clear-session',
    },
    { apiName: this.apiName,...config });
}