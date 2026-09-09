import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ChatMessageChunkDto, GenerateIepInputDto, GetIepListInputDto, IepPlanDto, ReviewIepInputDto, SaveIepInputDto, SpecialEduContentVersionDto, SubmitIepForReviewInputDto, UpdateIepContentDto } from '../../special-education/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class SpecialIepService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/special-iep/${id}`,
    },
    { apiName: this.apiName,...config });
  

  exportDocxByResultJsonAndStudentName = (resultJson: string, studentName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'POST',
      url: '/api/app/special-iep/export-docx',
      params: { resultJson, studentName },
    },
    { apiName: this.apiName,...config });
  

  generateStreaming = (input: GenerateIepInputDto, onChunk: any<ChatMessageChunkDto, any>, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/special-iep/generate-streaming',
      body: onChunk,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IepPlanDto>({
      method: 'GET',
      url: `/api/app/special-iep/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetIepListInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IepPlanDto>>({
      method: 'GET',
      url: '/api/app/special-iep',
      params: { category: input.category, status: input.status, studentUserId: input.studentUserId, keyword: input.keyword, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyIepList = (input: GetIepListInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IepPlanDto>>({
      method: 'GET',
      url: '/api/app/special-iep/my-iep-list',
      params: { category: input.category, status: input.status, studentUserId: input.studentUserId, keyword: input.keyword, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getVersions = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduContentVersionDto[]>({
      method: 'GET',
      url: `/api/app/special-iep/${id}/versions`,
    },
    { apiName: this.apiName,...config });
  

  review = (input: ReviewIepInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IepPlanDto>({
      method: 'POST',
      url: '/api/app/special-iep/review',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  save = (input: SaveIepInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IepPlanDto>({
      method: 'POST',
      url: '/api/app/special-iep/save',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submitForReview = (input: SubmitIepForReviewInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IepPlanDto>({
      method: 'POST',
      url: '/api/app/special-iep/submit-for-review',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateContent = (input: UpdateIepContentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IepPlanDto>({
      method: 'PUT',
      url: '/api/app/special-iep/content',
      body: input,
    },
    { apiName: this.apiName,...config });
}