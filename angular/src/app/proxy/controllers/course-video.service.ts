import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class CourseVideoService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  streamById = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/course-video/${id}/stream`,
    },
    { apiName: this.apiName,...config });
}