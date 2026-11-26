import type { WhiteboardAssistantAskInputDto, WhiteboardAssistantHistoryItemDto, WhiteboardAssistantReplyDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WhiteboardAssistantService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  ask = (input: WhiteboardAssistantAskInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WhiteboardAssistantReplyDto>({
      method: 'POST',
      url: '/api/app/whiteboard-assistant/ask',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  clearHistory = (whiteboardId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/whiteboard-assistant/clear-history/${whiteboardId}`,
    },
    { apiName: this.apiName,...config });
  

  getHistory = (whiteboardId: string, maxResultCount: number = 40, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WhiteboardAssistantHistoryItemDto[]>({
      method: 'GET',
      url: `/api/app/whiteboard-assistant/history/${whiteboardId}`,
      params: { maxResultCount },
    },
    { apiName: this.apiName,...config });
}