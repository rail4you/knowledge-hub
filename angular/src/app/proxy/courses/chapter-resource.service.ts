import type { ChapterResourceDto, CreateChapterResourceDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ChapterResourceService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateChapterResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChapterResourceDto>({
      method: 'POST',
      url: '/api/app/chapter-resource',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/chapter-resource/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByChapter = (chapterId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChapterResourceDto[]>({
      method: 'GET',
      url: `/api/app/chapter-resource/by-chapter/${chapterId}`,
    },
    { apiName: this.apiName,...config });
}