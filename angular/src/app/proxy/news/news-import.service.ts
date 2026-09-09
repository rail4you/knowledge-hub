import type { NewsImportResultDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

@Injectable({
  providedIn: 'root',
})
export class NewsImportService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  downloadTemplate = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      url: '/api/app/news-import/download-template',
    },
    { apiName: this.apiName,...config });
  

  import = (file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsImportResultDto>({
      method: 'POST',
      url: '/api/app/news-import/import',
      body: file,
    },
    { apiName: this.apiName,...config });
}