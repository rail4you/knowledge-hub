import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { SearchDashboardDto, SearchStatsQueryDto } from '../contracts/search/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class SearchStatisticsService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getDashboard = (input: SearchStatsQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SearchDashboardDto>({
      method: 'GET',
      url: '/api/app/search-statistics/dashboard',
      params: { startDate: input.startDate, endDate: input.endDate, tenantId: input.tenantId },
    },
    { apiName: this.apiName,...config });
}