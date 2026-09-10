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
}