import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';

export interface OssUploadResultDto {
  url: string;
  objectKey: string;
  originalFileName: string;
  size: number;
}

@Injectable({
  providedIn: 'root',
})
export class OssUploadService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  /**
   * Upload an image file to Aliyun OSS via the backend.
   * Returns the public URL of the uploaded image.
   */
  uploadImage(file: File): Observable<OssUploadResultDto> {
    const formData = new FormData();
    formData.append('file', file);

    return this.restService.request<any, OssUploadResultDto>({
      method: 'POST',
      url: '/api/oss-upload/image',
      body: formData,
    }, { apiName: this.apiName });
  }

  /**
   * Upload an arbitrary file (PDF / Word / Excel / 视频 等) to Aliyun OSS.
   * 后端限制 50MB；返回公开 URL。
   */
  uploadFile(file: File): Observable<OssUploadResultDto> {
    const formData = new FormData();
    formData.append('file', file);

    return this.restService.request<any, OssUploadResultDto>({
      method: 'POST',
      url: '/api/oss-upload/file',
      body: formData,
    }, { apiName: this.apiName });
  }
}
