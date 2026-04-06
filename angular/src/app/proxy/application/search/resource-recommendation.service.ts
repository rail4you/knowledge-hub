import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { RecommendedResourceDto, ResourceStatisticsDto } from '../contracts/search/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class ResourceRecommendationService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getCategoryBasedRecommendations = (count: number = 6, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecommendedResourceDto[]>({
      method: 'GET',
      url: '/api/app/resource-recommendation/category-based-recommendations',
      params: { count },
    },
    { apiName: this.apiName,...config });
  

  getPersonalizedRecommendations = (count: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecommendedResourceDto[]>({
      method: 'GET',
      url: '/api/app/resource-recommendation/personalized-recommendations',
      params: { count },
    },
    { apiName: this.apiName,...config });
  

  getRelatedResources = (resourceId: string, count: number = 6, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecommendedResourceDto[]>({
      method: 'GET',
      url: `/api/app/resource-recommendation/related-resources/${resourceId}`,
      params: { count },
    },
    { apiName: this.apiName,...config });
  

  getResourceStatistics = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceStatisticsDto>({
      method: 'GET',
      url: `/api/app/resource-recommendation/resource-statistics/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getTrendingResources = (count: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecommendedResourceDto[]>({
      method: 'GET',
      url: '/api/app/resource-recommendation/trending-resources',
      params: { count },
    },
    { apiName: this.apiName,...config });
}