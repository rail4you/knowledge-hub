import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ChatMessageChunkDto, GenerateSpecialResourceInputDto, GetSpecialResourceListInputDto, ReviewSpecialResourceInputDto, SaveSpecialResourceInputDto, SpecialEduContentVersionDto, SpecialEduResourceDto, SubmitSpecialResourceForReviewInputDto, UpdateResourceContentDto } from '../../special-education/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class SpecialEduResourceService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  batchExport = (ids: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'POST',
      url: '/api/app/special-edu-resource/batch-export',
      body: ids,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/special-edu-resource/${id}`,
    },
    { apiName: this.apiName,...config });
  

  exportDocxByResultJsonAndModality = (resultJson: string, modality: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'POST',
      url: '/api/app/special-edu-resource/export-docx',
      params: { resultJson, modality },
    },
    { apiName: this.apiName,...config });
  

  generateStreaming = (input: GenerateSpecialResourceInputDto, onChunk: any<ChatMessageChunkDto, any>, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/special-edu-resource/generate-streaming',
      body: onChunk,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduResourceDto>({
      method: 'GET',
      url: `/api/app/special-edu-resource/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetSpecialResourceListInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SpecialEduResourceDto>>({
      method: 'GET',
      url: '/api/app/special-edu-resource',
      params: { category: input.category, modality: input.modality, excludeModality: input.excludeModality, status: input.status, keyword: input.keyword, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getVersions = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduContentVersionDto[]>({
      method: 'GET',
      url: `/api/app/special-edu-resource/${id}/versions`,
    },
    { apiName: this.apiName,...config });
  

  review = (input: ReviewSpecialResourceInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduResourceDto>({
      method: 'POST',
      url: '/api/app/special-edu-resource/review',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  save = (input: SaveSpecialResourceInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduResourceDto>({
      method: 'POST',
      url: '/api/app/special-edu-resource/save',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submitForReview = (input: SubmitSpecialResourceForReviewInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduResourceDto>({
      method: 'POST',
      url: '/api/app/special-edu-resource/submit-for-review',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateContent = (input: UpdateResourceContentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduResourceDto>({
      method: 'PUT',
      url: '/api/app/special-edu-resource/content',
      body: input,
    },
    { apiName: this.apiName,...config });
}