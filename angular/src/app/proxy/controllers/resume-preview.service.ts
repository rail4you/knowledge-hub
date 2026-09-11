import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class ResumePreviewService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  preview = (url: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/app/resume-preview',
      params: { url },
    },
    { apiName: this.apiName,...config });
}