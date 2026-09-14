import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';

export interface UploadFirstFrameResult {
  url: string;
  fileName: string;
}

@Injectable({ providedIn: 'root' })
export class AiMediaUploadService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  uploadFirstFrame = (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return this.restService.request<FormData, UploadFirstFrameResult>(
      { method: 'POST', url: '/api/learning/ai/upload-first-frame', body: formData },
      { apiName: this.apiName },
    );
  };
}