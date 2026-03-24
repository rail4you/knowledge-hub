import type { ChatInputDto, ChatMessageChunkDto, ChatThreadDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ChatService {
  private restService = inject(RestService);
  apiName = 'Default';

  chatStreaming(input: ChatInputDto, config?: Partial<Rest.Config>) {
    return this.restService.request<any, ChatMessageChunkDto>({
      method: 'POST',
      url: '/api/app/chat/chat-streaming',
      body: input,
    }, { apiName: this.apiName, ...config });
  }

  createThread(config?: Partial<Rest.Config>) {
    return this.restService.request<any, ChatThreadDto>({
      method: 'POST',
      url: '/api/app/chat/thread',
    }, { apiName: this.apiName, ...config });
  }

  getMyThreads(config?: Partial<Rest.Config>) {
    return this.restService.request<any, ChatThreadDto[]>({
      method: 'GET',
      url: '/api/app/chat/my-threads',
    }, { apiName: this.apiName, ...config });
  }

  getThread(threadId: string, config?: Partial<Rest.Config>) {
    return this.restService.request<any, ChatThreadDto>({
      method: 'GET',
      url: `/api/app/chat/thread/${threadId}`,
    }, { apiName: this.apiName, ...config });
  }
}