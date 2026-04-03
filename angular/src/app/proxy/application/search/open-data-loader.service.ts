import type { OpenDataLoaderResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { PageContentDto } from '../contracts/search/models';

@Injectable({
  providedIn: 'root',
})
export class OpenDataLoaderService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  extractPages = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageContentDto[]>({
      method: 'POST',
      url: `/api/app/open-data-loader/extract-pages/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  parseDocument = (filePath: string, ct?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpenDataLoaderResultDto>({
      method: 'POST',
      url: '/api/app/open-data-loader/parse-document',
      params: { filePath },
    },
    { apiName: this.apiName,...config });
}