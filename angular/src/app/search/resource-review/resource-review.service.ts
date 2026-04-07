import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RestService } from '@abp/ng.core';

export interface ResourceReviewDto {
  id: string;
  resourceId: string;
  userId: string;
  userName: string;
  rating: number;
  content: string | null;
  creationTime: string;
}

export interface CreateResourceReviewInput {
  resourceId: string;
  rating: number;
  content?: string;
}

export interface UpdateResourceReviewInput {
  rating: number;
  content?: string;
}

export interface ResourceRatingSummaryDto {
  resourceId: string;
  averageRating: number;
  totalReviews: number;
  ratingDistribution: number[];
  myReview: ResourceReviewDto | null;
}

@Injectable({ providedIn: 'root' })
export class ResourceReviewService {
  private readonly restService = inject(RestService);
  private readonly apiUrl = '/api/app/resource-review';

  create(input: CreateResourceReviewInput): Observable<ResourceReviewDto> {
    return this.restService.request({ method: 'POST', url: this.apiUrl, body: input }, { apiName: 'KnowledgeHub' });
  }

  update(id: string, input: UpdateResourceReviewInput): Observable<ResourceReviewDto> {
    return this.restService.request({ method: 'PUT', url: `${this.apiUrl}/${id}`, body: input }, { apiName: 'KnowledgeHub' });
  }

  delete(id: string): Observable<void> {
    return this.restService.request({ method: 'DELETE', url: `${this.apiUrl}/${id}` }, { apiName: 'KnowledgeHub' });
  }

  getMyReview(resourceId: string): Observable<ResourceReviewDto | null> {
    return this.restService.request({ method: 'GET', url: `${this.apiUrl}/my-review/${resourceId}` }, { apiName: 'KnowledgeHub' });
  }

  getResourceReviews(resourceId: string, skipCount = 0, maxResultCount = 20): Observable<ResourceReviewDto[]> {
    return this.restService.request({ method: 'GET', url: `${this.apiUrl}/resource-reviews/${resourceId}`, params: { skipCount, maxResultCount } }, { apiName: 'KnowledgeHub' });
  }

  getRatingSummary(resourceId: string): Observable<ResourceRatingSummaryDto> {
    return this.restService.request({ method: 'GET', url: `${this.apiUrl}/rating-summary/${resourceId}` }, { apiName: 'KnowledgeHub' });
  }
}
