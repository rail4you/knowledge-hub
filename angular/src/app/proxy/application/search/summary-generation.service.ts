import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BatchGenerateSummaryInputDto, BatchGenerateSummaryResultDto, GenerateSummaryInputDto, GenerateSummaryResultDto } from '../contracts/search/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class SummaryGenerationService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  batchGenerate = (input: BatchGenerateSummaryInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchGenerateSummaryResultDto>({
      method: 'POST',
      url: '/api/app/summary-generation/batch-generate',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  generateForResource = (input: GenerateSummaryInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GenerateSummaryResultDto>({
      method: 'POST',
      url: '/api/app/summary-generation/generate-for-resource',
      body: input,
    },
    { apiName: this.apiName,...config });
}