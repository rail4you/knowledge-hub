import type { ResumeUploadResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

@Injectable({
  providedIn: 'root',
})
export class ResumeUploadService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  upload = (file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResumeUploadResultDto>({
      method: 'POST',
      url: '/api/app/resume-upload',
      body: file,
    },
    { apiName: this.apiName,...config });
}