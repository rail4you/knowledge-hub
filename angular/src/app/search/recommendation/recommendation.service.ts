import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RestService } from '@abp/ng.core';

export interface RecommendedResourceDto {
  resourceId: string;
  resourceName: string;
  description: string | null;
  resourceType: number;
  categoryId: string | null;
  categoryName: string | null;
  keywords: string | null;
  fileExtension: string | null;
  fileSize: number | null;
  viewCount: number;
  collectionCount: number;
  downloadCount: number;
  averageRating: number;
  totalReviews: number;
  recommendationScore: number;
  recommendationReason: string;
  creationTime: string;
}

export interface ResourceStatisticsDto {
  resourceId: string;
  totalViews: number;
  uniqueViewers: number;
  avgViewDurationSeconds: number;
  totalDownloads: number;
  totalCollections: number;
  collectionRate: number;
  downloadRate: number;
  averageRating: number;
  totalReviews: number;
  ratingDistribution: number[];
  viewsLast30Days: number;
  viewsPrevious30Days: number;
  viewTrendPercentage: number;
  timesInSearchResults: number;
  timesClickedFromSearch: number;
  clickThroughRate: number;
}

@Injectable({ providedIn: 'root' })
export class RecommendationService {
  private readonly restService = inject(RestService);
  private readonly apiUrl = '/api/app/resource-recommendation';

  getPersonalizedRecommendations(count = 10): Observable<RecommendedResourceDto[]> {
    return this.restService.request(
      { method: 'GET', url: `${this.apiUrl}/personalized-recommendations`, params: { count } },
      { apiName: 'Default' }
    );
  }

  getRelatedResources(resourceId: string, count = 6): Observable<RecommendedResourceDto[]> {
    return this.restService.request(
      { method: 'GET', url: `${this.apiUrl}/related-resources/${resourceId}`, params: { count } },
      { apiName: 'Default' }
    );
  }

  getResourceStatistics(resourceId: string): Observable<ResourceStatisticsDto> {
    return this.restService.request(
      { method: 'GET', url: `${this.apiUrl}/resource-statistics/${resourceId}` },
      { apiName: 'Default' }
    );
  }

  getTrendingResources(count = 10): Observable<RecommendedResourceDto[]> {
    return this.restService.request(
      { method: 'GET', url: `${this.apiUrl}/trending-resources`, params: { count } },
      { apiName: 'Default' }
    );
  }

  getCategoryBasedRecommendations(count = 6): Observable<RecommendedResourceDto[]> {
    return this.restService.request(
      { method: 'GET', url: `${this.apiUrl}/category-based-recommendations`, params: { count } },
      { apiName: 'Default' }
    );
  }
}
