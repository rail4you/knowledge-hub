import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';
import type { CompleteUploadDto, CompleteUploadResultDto, InitiateUploadDto, InitiateUploadResultDto } from '../resources/models';

@Injectable({
  providedIn: 'root',
})
export class ChunkUploadService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  completeUploadByInput = (input: CompleteUploadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompleteUploadResultDto>({
      method: 'POST',
      url: '/api/app/chunk-upload/complete',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  initiateUploadByInput = (input: InitiateUploadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InitiateUploadResultDto>({
      method: 'POST',
      url: '/api/app/chunk-upload/initiate',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  uploadChunkByFileAndUploadIdAndFileNameAndChunkNumber = (file: IFormFile, uploadId: string, fileName: string, chunkNumber: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: '/api/app/chunk-upload/upload',
      body: file,
    },
    { apiName: this.apiName,...config });
}