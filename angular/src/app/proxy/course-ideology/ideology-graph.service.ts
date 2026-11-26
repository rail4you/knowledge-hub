import type { IdeologyGraphDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IdeologyGraphService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  get = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyGraphDto>({
      method: 'GET',
      url: '/api/app/ideology-graph',
      params: { courseId },
    },
    { apiName: this.apiName,...config });
}