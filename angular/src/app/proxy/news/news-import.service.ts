import type { NewsImportResultDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class NewsImportService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  import = (excelFile: number[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, NewsImportResultDto>({
      method: 'POST',
      url: '/api/app/news-import/import',
      body: excelFile,
    },
    { apiName: this.apiName,...config });
}