import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateResourceReviewDto, ResourceRatingSummaryDto, ResourceReviewDto, UpdateResourceReviewDto } from '../contracts/search/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class ResourceReviewService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateResourceReviewDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceReviewDto>({
      method: 'POST',
      url: '/api/app/resource-review',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource-review/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getMyReview = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceReviewDto>({
      method: 'GET',
      url: `/api/app/resource-review/my-review/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getRatingSummary = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceRatingSummaryDto>({
      method: 'GET',
      url: `/api/app/resource-review/rating-summary/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getResourceReviews = (resourceId: string, skipCount?: number, maxResultCount: number = 20, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceReviewDto[]>({
      method: 'GET',
      url: `/api/app/resource-review/resource-reviews/${resourceId}`,
      params: { skipCount, maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateResourceReviewDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceReviewDto>({
      method: 'PUT',
      url: `/api/app/resource-review/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}