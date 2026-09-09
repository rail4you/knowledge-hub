import type { ChatInputDto, ChatMessageChunkDto, ChatMessageDto, ChatThreadDto, ResourceForChatDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ChatService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  chatStreaming = (input: ChatInputDto, onChunk: any<ChatMessageChunkDto, any>, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/chat/chat-streaming',
      body: onChunk,
    },
    { apiName: this.apiName,...config });
  

  clearAllThreads = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/chat/clear-all-threads',
    },
    { apiName: this.apiName,...config });
  

  createThread = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChatThreadDto>({
      method: 'POST',
      url: '/api/app/chat/thread',
    },
    { apiName: this.apiName,...config });
  

  deleteThread = (threadId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/chat/thread/${threadId}`,
    },
    { apiName: this.apiName,...config });
  

  getMyThreads = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChatThreadDto[]>({
      method: 'GET',
      url: '/api/app/chat/my-threads',
    },
    { apiName: this.apiName,...config });
  

  getResourcesWithPageIndex = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceForChatDto[]>({
      method: 'GET',
      url: '/api/app/chat/resources-with-page-index',
    },
    { apiName: this.apiName,...config });
  

  getThread = (threadId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChatThreadDto>({
      method: 'GET',
      url: `/api/app/chat/thread/${threadId}`,
    },
    { apiName: this.apiName,...config });
  

  saveMessages = (threadId: string, title: string, resourceId: string, messages: ChatMessageDto[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/chat/save-messages',
      params: { threadId, title, resourceId },
      body: messages,
    },
    { apiName: this.apiName,...config });
}