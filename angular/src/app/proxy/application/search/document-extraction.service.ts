import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { PageContentDto } from '../contracts/search/models';

@Injectable({
  providedIn: 'root',
})
export class DocumentExtractionService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  extractPages = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageContentDto[]>({
      method: 'POST',
      url: `/api/app/document-extraction/extract-pages/${resourceId}`,
    },
    { apiName: this.apiName,...config });
}