import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EmbeddingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  generateBatchEmbeddings = (texts: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'POST',
      url: '/api/app/embedding/generate-batch-embeddings',
      body: texts,
    },
    { apiName: this.apiName,...config });
  

  generateEmbedding = (text: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number[]>({
      method: 'POST',
      url: '/api/app/embedding/generate-embedding',
      params: { text },
    },
    { apiName: this.apiName,...config });
}