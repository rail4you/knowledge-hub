import type { PracticeResultDto, PracticeSetDto, SubmitPracticeInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PracticeService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getSet = (courseId: string, knowledgePoint?: string, take: number = 6, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticeSetDto>({
      method: 'GET',
      url: `/api/app/practice/set/${courseId}`,
      params: { knowledgePoint, take },
    },
    { apiName: this.apiName,...config });
  

  submit = (input: SubmitPracticeInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticeResultDto>({
      method: 'POST',
      url: '/api/app/practice/submit',
      body: input,
    },
    { apiName: this.apiName,...config });
}