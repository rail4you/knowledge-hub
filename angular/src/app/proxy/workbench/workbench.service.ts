import type { WorkbenchQueryDto, WorkbenchStatsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WorkbenchService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getStats = (input: WorkbenchQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkbenchStatsDto>({
      method: 'GET',
      url: '/api/app/workbench/stats',
      params: { tenantId: input.tenantId },
    },
    { apiName: this.apiName,...config });
}