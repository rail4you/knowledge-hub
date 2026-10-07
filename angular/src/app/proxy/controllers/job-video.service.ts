import type { JobVideoUploadResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class JobVideoService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  get = (path: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/app/job-video',
      params: { path },
    },
    { apiName: this.apiName,...config });
  

  preview = (path: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/app/job-video/preview',
      params: { path },
    },
    { apiName: this.apiName,...config });
  

  upload = (file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobVideoUploadResultDto>({
      method: 'POST',
      url: '/api/app/job-video/upload',
      body: file,
    },
    { apiName: this.apiName,...config });
}