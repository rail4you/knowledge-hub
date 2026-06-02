import type { KnowledgeGraphDto, KnowledgeNodeDto, KnowledgeRelationDto, LearningPathDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class KnowledgeGraphService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  buildGraph = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/knowledge-graph/build-graph/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getGraph = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeGraphDto>({
      method: 'GET',
      url: `/api/app/knowledge-graph/graph/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getMicroMajorGraph = (microMajorId: string, microMajorName: string, courseIds: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeGraphDto>({
      method: 'GET',
      url: `/api/app/knowledge-graph/micro-major-graph/${microMajorId}`,
      params: { microMajorName, courseIds },
    },
    { apiName: this.apiName,...config });
  

  getNode = (nodeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeNodeDto>({
      method: 'GET',
      url: `/api/app/knowledge-graph/node/${nodeId}`,
    },
    { apiName: this.apiName,...config });
  

  getRecommendedPath = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LearningPathDto>({
      method: 'GET',
      url: `/api/app/knowledge-graph/recommended-path/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getRelations = (nodeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeRelationDto[]>({
      method: 'GET',
      url: `/api/app/knowledge-graph/relations/${nodeId}`,
    },
    { apiName: this.apiName,...config });
  

  getResourceGraph = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgeGraphDto>({
      method: 'GET',
      url: `/api/app/knowledge-graph/resource-graph/${resourceId}`,
    },
    { apiName: this.apiName,...config });
}