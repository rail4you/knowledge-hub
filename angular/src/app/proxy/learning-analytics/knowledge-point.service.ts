import type { AddKpExerciseLinkInput, GetKnowledgePointListInput, KnowledgePointDetailDto, KnowledgePointGraphDto, KnowledgePointMergeResultDto, KnowledgeVectorStatusDto, KpExerciseLinkDto, KpMergeSuggestionDto, MergeKnowledgePointsInput, RebuildEmbeddingsResultDto, SplitKnowledgePointInput, UpdateKnowledgePointInput, UpdateKpExerciseLinkInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class KnowledgePointService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  addExerciseLink = (input: AddKpExerciseLinkInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KpExerciseLinkDto>({
      method: 'POST',
      url: '/api/app/knowledge-point/exercise-link',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  confirm = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgePointDetailDto>({
      method: 'POST',
      url: `/api/app/knowledge-point/${id}/confirm`,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/knowledge-point/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgePointDetailDto>({
      method: 'GET',
      url: `/api/app/knowledge-point/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getExerciseLinks = (knowledgePointId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KpExerciseLinkDto[]>({
      method: 'GET',
      url: `/api/app/knowledge-point/exercise-links/${knowledgePointId}`,
    },
    { apiName: this.apiName,...config });
  

  getGraph = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgePointGraphDto>({
      method: 'GET',
      url: `/api/app/knowledge-point/graph/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetKnowledgePointListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<KnowledgePointDetailDto>>({
      method: 'GET',
      url: '/api/app/knowledge-point',
      params: { courseId: input.courseId, filter: input.filter, status: input.status, source: input.source, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMergeSuggestions = (courseId: string, threshold: number = 0.75, scope: string = "course", config?: Partial<Rest.Config>) =>
    this.restService.request<any, KpMergeSuggestionDto[]>({
      method: 'GET',
      url: `/api/app/knowledge-point/merge-suggestions/${courseId}`,
      params: { threshold, scope },
    },
    { apiName: this.apiName,...config });
  

  getVectorStatus = (courseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeVectorStatusDto>({
      method: 'GET',
      url: '/api/app/knowledge-point/vector-status',
      params: { courseId },
    },
    { apiName: this.apiName,...config });
  

  merge = (input: MergeKnowledgePointsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgePointMergeResultDto>({
      method: 'POST',
      url: '/api/app/knowledge-point/merge',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  rebuildEmbeddings = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RebuildEmbeddingsResultDto>({
      method: 'POST',
      url: `/api/app/knowledge-point/rebuild-embeddings/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  removeExerciseLink = (linkId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/knowledge-point/exercise-link/${linkId}`,
    },
    { apiName: this.apiName,...config });
  

  split = (input: SplitKnowledgePointInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgePointDetailDto>({
      method: 'POST',
      url: '/api/app/knowledge-point/split',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateKnowledgePointInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgePointDetailDto>({
      method: 'PUT',
      url: `/api/app/knowledge-point/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateExerciseLink = (input: UpdateKpExerciseLinkInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KpExerciseLinkDto>({
      method: 'PUT',
      url: '/api/app/knowledge-point/exercise-link',
      body: input,
    },
    { apiName: this.apiName,...config });
}