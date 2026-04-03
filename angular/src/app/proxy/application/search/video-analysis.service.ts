import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { VideoAnalysisRequestDto, VideoAnalysisResultDto } from '../contracts/search/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class VideoAnalysisService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  analyzeLocalVideo = (filePath: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VideoAnalysisResultDto>({
      method: 'POST',
      url: '/api/app/video-analysis/analyze-local-video',
      params: { filePath },
    },
    { apiName: this.apiName,...config });
  

  analyzeVideoTimeline = (input: VideoAnalysisRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VideoAnalysisResultDto>({
      method: 'POST',
      url: '/api/app/video-analysis/analyze-video-timeline',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  saveVideoTimelineToMeiliSearch = (videoId: string, videoName: string, videoUrl: string, analysisResult: VideoAnalysisResultDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/video-analysis/save-video-timeline-to-meili-search/${videoId}`,
      params: { videoName, videoUrl },
      body: analysisResult,
    },
    { apiName: this.apiName,...config });
}