import type { GenerateIdeologyCaseInputDto, IdeologyCaseDraftDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IdeologyAiService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  generateDraft = (input: GenerateIdeologyCaseInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdeologyCaseDraftDto>({
      method: 'POST',
      url: '/api/app/ideology-ai/generate-draft',
      body: input,
    },
    { apiName: this.apiName,...config });
}