import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RestService } from '@abp/ng.core';

export interface SearchDashboardDto {
  totalSearches: number;
  todaySearches: number;
  activeUsers: number;
  todayActiveUsers: number;
  dailyTrends: DailySearchTrendDto[];
  popularSearches: PopularSearchTermDto[];
  topResources: TopResourceStatsDto[];
  topRatedResources: TopRatedResourceDto[];
}

export interface SearchStatsQueryDto {
  startDate?: string;
  endDate?: string;
  tenantId?: string;
}

export interface DailySearchTrendDto {
  date: string;
  searchCount: number;
  uniqueUsers: number;
}

export interface PopularSearchTermDto {
  keyword: string;
  count: number;
}

export interface TopResourceStatsDto {
  resourceId: string;
  resourceName: string;
  searchCount: number;
  clickCount: number;
  clickRate: number;
}

export interface TopRatedResourceDto {
  resourceId: string;
  resourceName: string;
  averageRating: number;
  reviewCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class SearchStatisticsService {
  private readonly restService = inject(RestService);
  private readonly apiUrl = '/api/app/search-statistics';

  getDashboard(input: SearchStatsQueryDto): Observable<SearchDashboardDto> {
    const params: Record<string, any> = {};
    if (input.startDate) params.startDate = input.startDate;
    if (input.endDate) params.endDate = input.endDate;
    if (input.tenantId) params.tenantId = input.tenantId;

    return this.restService.request({
      method: 'GET',
      url: `${this.apiUrl}/dashboard`,
      params
    }, { apiName: 'Default' });
  }
}
