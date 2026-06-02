import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class ResourceFileService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  downloadByResourceId = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/resource-file/${resourceId}/download`,
    },
    { apiName: this.apiName,...config });
  

  previewByResourceId = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/resource-file/${resourceId}/preview`,
    },
    { apiName: this.apiName,...config });
}