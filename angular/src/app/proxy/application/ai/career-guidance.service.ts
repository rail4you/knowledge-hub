import type { CareerGuidanceGenerationInputDto, ChatMessageChunkDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CareerGuidanceService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  exportDocxByCareerGuidanceJson = (careerGuidanceJson: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'POST',
      url: '/api/app/career-guidance/export-docx',
      params: { careerGuidanceJson },
    },
    { apiName: this.apiName,...config });
  

  generateStreaming = (input: CareerGuidanceGenerationInputDto, onChunk: (chunk: ChatMessageChunkDto) => any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/career-guidance/generate-streaming',
      body: input,
    },
    { apiName: this.apiName,...config });
}