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
  

  previewByResourceIdAndCountView = (resourceId: string, countView: boolean = true, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/resource-file/${resourceId}/preview`,
      params: { countView },
    },
    { apiName: this.apiName,...config });
  

  previewPdfByResourceId = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/resource-file/${resourceId}/preview-pdf`,
    },
    { apiName: this.apiName,...config });
  

  previewPdfInfoByResourceId = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/resource-file/${resourceId}/preview-pdf-info`,
    },
    { apiName: this.apiName,...config });
  

  thumbnailByResourceIdAndW = (resourceId: string, w: number = 400, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/resource-file/${resourceId}/thumbnail`,
      params: { w },
    },
    { apiName: this.apiName,...config });
}