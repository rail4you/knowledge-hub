import { Injectable, inject } from '@angular/core';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';

/**
 * 学生端就业模块的**原生导航 URL 构造**。
 *
 * 说明（与生成代理的分工）：
 *   · 上传 / 预览的**请求**一律走生成代理：
 *       proxy/controllers/resume-upload.service.ts   → ResumeUploadService.upload
 *       proxy/controllers/resume-preview.service.ts  → ResumePreviewService.preview
 *       proxy/controllers/job-video.service.ts       → JobVideoService.get / preview
 *     这三个端点都是独立的 Controller，**已经在代理里**，本文件不再重复封装。
 *   · 本文件只负责构造**浏览器原生导航**用的 URL（iframe / window.open / <video src>）：
 *     原生导航无法携带 Authorization 请求头，所以预览地址必须在 URL 上附 access_token。
 *
 * 之前这里重复实现了三个端点，注释还声称「不在代理里」——已更正。
 */
@Injectable({ providedIn: 'root' })
export class StudentEmploymentFilesService {
  private readonly authFetch = inject(AuthFetchService);

  /**
   * 简历附件预览地址。
   * 附件存于 OSS，直接打开公网 URL 会变成下载（Word 也无原生预览），
   * 需经后端 /api/app/resume-preview 中转；该端点是浏览器原生导航
   * （iframe / window.open）无法带请求头，故在 URL 上附 access_token。
   */
  resumePreviewUrl(attachmentUrl: string): string {
    return this.authFetch.withAccessToken(
      `/api/app/resume-preview?url=${encodeURIComponent(attachmentUrl)}`,
    );
  }

  /** 岗位宣传视频地址（已发布岗位匿名可播） */
  jobVideoUrl(path: string): string {
    return `/api/app/job-video?path=${encodeURIComponent(path)}`;
  }
}
