import type { KnowledgeResourceDto, RelatedCoursesResultDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class KnowledgeResourceService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

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
  

  getRelatedCourses = (knowledgeResourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RelatedCoursesResultDto>({
      method: 'GET',
      url: `/api/app/knowledge-resource/related-courses/${knowledgeResourceId}`,
    },
    { apiName: this.apiName,...config });
}