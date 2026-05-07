import type { CaseAnalysisGenerationInputDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CaseAnalysisService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  exportDocxByCaseAnalysisJson = (caseAnalysisJson: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'POST',
      url: '/api/app/case-analysis/export-docx',
      params: { caseAnalysisJson },
    },
    { apiName: this.apiName,...config });
  

  generateStreaming = (input: CaseAnalysisGenerationInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/case-analysis/generate-streaming',
      body: input,
    },
    { apiName: this.apiName, ...config });
}