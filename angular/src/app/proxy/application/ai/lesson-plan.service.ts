import type { ChatMessageChunkDto, LessonPlanGenerationInputDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LessonPlanService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  exportDocxByLessonPlanJson = (lessonPlanJson: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'POST',
      url: '/api/app/lesson-plan/export-docx',
      params: { lessonPlanJson },
    },
    { apiName: this.apiName,...config });
  

  generateStreaming = (input: LessonPlanGenerationInputDto, onChunk: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/lesson-plan/generate-streaming',
      body: onChunk,
    },
    { apiName: this.apiName,...config });
}