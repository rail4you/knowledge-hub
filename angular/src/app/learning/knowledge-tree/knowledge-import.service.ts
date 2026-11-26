import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import type { KnowledgeResourceImportResultDto } from '../../proxy/courses/dtos/models';

/**
 * 知识点 xlsx 导入（multipart）。
 *
 * 为什么不走生成的 proxy：ABP CLI 对 `IFormFile` 参数生成的是 `body: file` + `params`（JSON），
 * 服务端会判 415。与作业上传、课程视频上传一样，这里手写 FormData。
 */
@Injectable({ providedIn: 'root' })
export class KnowledgeImportService {
  private readonly http = inject(HttpClient);

  upload(courseId: string, file: File): Promise<KnowledgeResourceImportResultDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    return new Promise((resolve, reject) => {
      this.http
        .post<KnowledgeResourceImportResultDto>(
          `/api/app/knowledge-resource/import-from-excel/${courseId}`,
          form,
        )
        .subscribe({ next: resolve, error: reject });
    });
  }

  /** 模板下载（IRemoteStreamContent，需要令牌 → 走原生 fetch + blob） */
  templateUrl(): string {
    return '/api/app/knowledge-resource/import-template';
  }
}
