import type { BatchReorderKnowledgeResourcesInput, CreateUpdateKnowledgeResourceDto, KnowledgeResourceDto, KnowledgeResourceImportResultDto, MoveKnowledgeResourceInput, RelatedCoursesResultDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

@Injectable({
  providedIn: 'root',
})
export class KnowledgeResourceService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  batchReorder = (input: BatchReorderKnowledgeResourcesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeResourceDto[]>({
      method: 'POST',
      url: '/api/app/knowledge-resource/batch-reorder',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateKnowledgeResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeResourceDto>({
      method: 'POST',
      url: '/api/app/knowledge-resource',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/knowledge-resource/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByChapter = (chapterId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeResourceDto[]>({
      method: 'GET',
      url: `/api/app/knowledge-resource/by-chapter/${chapterId}`,
    },
    { apiName: this.apiName,...config });
  

  getByCourse = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeResourceDto[]>({
      method: 'GET',
      url: `/api/app/knowledge-resource/by-course/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getImportTemplate = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      url: '/api/app/knowledge-resource/import-template',
    },
    { apiName: this.apiName,...config });
  

  getRelatedCourses = (knowledgeResourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RelatedCoursesResultDto>({
      method: 'GET',
      url: `/api/app/knowledge-resource/related-courses/${knowledgeResourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getTree = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeResourceDto[]>({
      method: 'GET',
      url: `/api/app/knowledge-resource/tree/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  importFromExcel = (courseId: string, file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeResourceImportResultDto>({
      method: 'POST',
      url: `/api/app/knowledge-resource/import-from-excel/${courseId}`,
      body: file,
    },
    { apiName: this.apiName,...config });
  

  move = (id: string, input: MoveKnowledgeResourceInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeResourceDto>({
      method: 'POST',
      url: `/api/app/knowledge-resource/${id}/move`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  regenerateSortPaths = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'POST',
      url: `/api/app/knowledge-resource/regenerate-sort-paths/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateKnowledgeResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeResourceDto>({
      method: 'PUT',
      url: `/api/app/knowledge-resource/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}