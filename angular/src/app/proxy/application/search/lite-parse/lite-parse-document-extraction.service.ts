import type { LiteParseExtractionResult } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { PageContentDto } from '../../contracts/search/models';

@Injectable({
  providedIn: 'root',
})
export class LiteParseDocumentExtractionService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  extractPages = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageContentDto[]>({
      method: 'POST',
      url: `/api/app/lite-parse-document-extraction/extract-pages/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  extractWithLayout = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LiteParseExtractionResult>({
      method: 'POST',
      url: `/api/app/lite-parse-document-extraction/extract-with-layout/${resourceId}`,
    },
    { apiName: this.apiName,...config });
}