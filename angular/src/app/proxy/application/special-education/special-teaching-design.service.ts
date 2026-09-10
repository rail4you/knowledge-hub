import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { GetTeachingDesignListInputDto, ReviewTeachingDesignInputDto, SaveTeachingDesignInputDto, SpecialEduContentVersionDto, SpecialTeachingDesignDto, SubmitTeachingDesignForReviewInputDto, UpdateTeachingDesignContentDto } from '../../special-education/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class SpecialTeachingDesignService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/special-teaching-design/${id}`,
    },
    { apiName: this.apiName,...config });
  

  exportDocxByResultJson = (resultJson: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'POST',
      url: '/api/app/special-teaching-design/export-docx',
      params: { resultJson },
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialTeachingDesignDto>({
      method: 'GET',
      url: `/api/app/special-teaching-design/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetTeachingDesignListInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SpecialTeachingDesignDto>>({
      method: 'GET',
      url: '/api/app/special-teaching-design',
      params: { category: input.category, status: input.status, keyword: input.keyword, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getVersions = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduContentVersionDto[]>({
      method: 'GET',
      url: `/api/app/special-teaching-design/${id}/versions`,
    },
    { apiName: this.apiName,...config });
  

  review = (input: ReviewTeachingDesignInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialTeachingDesignDto>({
      method: 'POST',
      url: '/api/app/special-teaching-design/review',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  save = (input: SaveTeachingDesignInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialTeachingDesignDto>({
      method: 'POST',
      url: '/api/app/special-teaching-design/save',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submitForReview = (input: SubmitTeachingDesignForReviewInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialTeachingDesignDto>({
      method: 'POST',
      url: '/api/app/special-teaching-design/submit-for-review',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateContent = (input: UpdateTeachingDesignContentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialTeachingDesignDto>({
      method: 'PUT',
      url: '/api/app/special-teaching-design/content',
      body: input,
    },
    { apiName: this.apiName,...config });
}