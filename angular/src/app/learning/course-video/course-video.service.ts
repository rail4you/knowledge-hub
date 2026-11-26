import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import type { CourseVideoDto } from '../../proxy/courses/dtos/models';

/**
 * 课程视频上传（multipart）。
 *
 * 为什么不走生成的 proxy：ABP CLI 对 `IFormFile` 参数生成的是
 * `body: file` + `params: {...}`（JSON body），服务端会判 415。
 * 与作业上传、资源上传一样，这里手写 FormData。
 */
@Injectable({ providedIn: 'root' })
export class CourseVideoUploadService {
  private readonly http = inject(HttpClient);

  upload(params: {
    courseId: string;
    file: File;
    title?: string;
    chapterId?: string | null;
    knowledgeResourceId?: string | null;
    description?: string | null;
    sortOrder?: number;
  }): Promise<CourseVideoDto> {
    const form = new FormData();
    form.append('file', params.file, params.file.name);

    // 非文件参数走 query（与服务端扁平参数签名一致）
    const query = new URLSearchParams({ courseId: params.courseId });
    if (params.title) query.set('title', params.title);
    if (params.chapterId) query.set('chapterId', params.chapterId);
    if (params.knowledgeResourceId) query.set('knowledgeResourceId', params.knowledgeResourceId);
    if (params.description) query.set('description', params.description);
    if (params.sortOrder !== undefined && params.sortOrder !== null) {
      query.set('sortOrder', String(params.sortOrder));
    }

    return new Promise((resolve, reject) => {
      this.http
        .post<CourseVideoDto>(`/api/app/course-video/upload?${query.toString()}`, form)
        .subscribe({ next: resolve, error: reject });
    });
  }
}
