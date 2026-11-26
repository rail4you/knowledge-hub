import type { CreateDiscussionInput, CreateDiscussionReplyInput, DiscussionDto, DiscussionReplyDto, GetDiscussionListInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DiscussionService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  acceptReply = (id: string, replyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DiscussionDto>({
      method: 'POST',
      url: `/api/app/discussion/${id}/accept-reply/${replyId}`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateDiscussionInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DiscussionDto>({
      method: 'POST',
      url: '/api/app/discussion',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/discussion/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteReply = (replyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/discussion/reply/${replyId}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DiscussionDto>({
      method: 'GET',
      url: `/api/app/discussion/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetDiscussionListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DiscussionDto>>({
      method: 'GET',
      url: '/api/app/discussion',
      params: { courseId: input.courseId, chapterId: input.chapterId, onlyUnresolved: input.onlyUnresolved, onlyMine: input.onlyMine, filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  reply = (input: CreateDiscussionReplyInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DiscussionReplyDto>({
      method: 'POST',
      url: '/api/app/discussion/reply',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  setPinned = (id: string, pinned: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DiscussionDto>({
      method: 'POST',
      url: `/api/app/discussion/${id}/set-pinned`,
      params: { pinned },
    },
    { apiName: this.apiName,...config });
  

  setResolved = (id: string, resolved: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DiscussionDto>({
      method: 'POST',
      url: `/api/app/discussion/${id}/set-resolved`,
      params: { resolved },
    },
    { apiName: this.apiName,...config });
}