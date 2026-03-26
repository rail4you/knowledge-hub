import type { LiteparseResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { PageContentDto } from '../contracts/search/models';

@Injectable({
  providedIn: 'root',
})
export class LiteparseService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  extractPages = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageContentDto[]>({
      method: 'POST',
      url: `/api/app/liteparse/extract-pages/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  parseDocument = (filePath: string, ct?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LiteparseResultDto>({
      method: 'POST',
      url: '/api/app/liteparse/parse-document',
      params: { filePath },
    },
    { apiName: this.apiName,...config });
}