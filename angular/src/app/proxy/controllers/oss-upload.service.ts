import type { OssUploadResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

@Injectable({
  providedIn: 'root',
})
export class OssUploadService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  uploadFileByFile = (file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OssUploadResultDto>({
      method: 'POST',
      url: '/api/oss-upload/file',
      body: file,
    },
    { apiName: this.apiName,...config });
  

  uploadImageByFile = (file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OssUploadResultDto>({
      method: 'POST',
      url: '/api/oss-upload/image',
      body: file,
    },
    { apiName: this.apiName,...config });
}