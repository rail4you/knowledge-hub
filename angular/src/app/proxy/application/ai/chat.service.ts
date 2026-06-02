import type { ChatInputDto, ChatMessageChunkDto, ResourceForChatDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ChatService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  chatStreaming = (input: ChatInputDto, onChunk: (chunk: ChatMessageChunkDto) => any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/chat/chat-streaming',
      body: onChunk,
    },
    { apiName: this.apiName,...config });
  

  getResourcesWithPageIndex = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceForChatDto[]>({
      method: 'GET',
      url: '/api/app/chat/resources-with-page-index',
    },
    { apiName: this.apiName,...config });
}