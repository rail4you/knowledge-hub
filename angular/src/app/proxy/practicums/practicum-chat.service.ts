import type { GetPracticumChatMessagesDto, PracticumChatMessageDto, SendPracticumChatMessageDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PracticumChatService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getMessages = (input: GetPracticumChatMessagesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumChatMessageDto[]>({
      method: 'GET',
      url: '/api/app/practicum-chat/messages',
      params: { projectId: input.projectId, beforeId: input.beforeId, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  send = (input: SendPracticumChatMessageDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumChatMessageDto>({
      method: 'POST',
      url: '/api/app/practicum-chat/send',
      body: input,
    },
    { apiName: this.apiName,...config });
}