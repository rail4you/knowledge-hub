import type { IdeologyCoverageDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IdeologyCoverageService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  export = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/ideology-coverage/export/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  get = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyCoverageDto>({
      method: 'GET',
      url: '/api/app/ideology-coverage',
      params: { courseId },
    },
    { apiName: this.apiName,...config });
}