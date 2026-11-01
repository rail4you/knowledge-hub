import type { DecideReviewInput, ReviewTicketDto, ReviewTicketListRequestDto } from './dtos/models';
import type { ReviewTargetType } from './review-target-type.enum';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ReviewTicketService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  decide = (input: DecideReviewInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReviewTicketDto>({
      method: 'POST',
      url: '/api/app/review-ticket/decide',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReviewTicketDto>({
      method: 'GET',
      url: `/api/app/review-ticket/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByTarget = (targetType: ReviewTargetType, targetId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReviewTicketDto[]>({
      method: 'GET',
      url: `/api/app/review-ticket/by-target/${targetId}`,
      params: { targetType },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: ReviewTicketListRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ReviewTicketDto>>({
      method: 'GET',
      url: '/api/app/review-ticket',
      params: { targetType: input.targetType, stage: input.stage, status: input.status, libraryId: input.libraryId, targetId: input.targetId, onlyMine: input.onlyMine, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyPendingCount = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: '/api/app/review-ticket/my-pending-count',
    },
    { apiName: this.apiName,...config });
  

  resubmit = (id: string, payloadJson?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReviewTicketDto>({
      method: 'POST',
      url: `/api/app/review-ticket/${id}/resubmit`,
      params: { payloadJson },
    },
    { apiName: this.apiName,...config });
  

  withdraw = (id: string, reason?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReviewTicketDto>({
      method: 'POST',
      url: `/api/app/review-ticket/${id}/withdraw`,
      params: { reason },
    },
    { apiName: this.apiName,...config });
}