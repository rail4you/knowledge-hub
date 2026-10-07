import type { ImageGenerationInputDto, MediaGenerationTaskDto, VideoGenerationInputDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AiMediaService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  generateImage = (input: ImageGenerationInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MediaGenerationTaskDto>({
      method: 'POST',
      url: '/api/app/ai-media/generate-image',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  generateVideo = (input: VideoGenerationInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MediaGenerationTaskDto>({
      method: 'POST',
      url: '/api/app/ai-media/generate-video',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getMediaTask = (taskId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MediaGenerationTaskDto>({
      method: 'GET',
      url: `/api/app/ai-media/media-task/${taskId}`,
    },
    { apiName: this.apiName,...config });
}