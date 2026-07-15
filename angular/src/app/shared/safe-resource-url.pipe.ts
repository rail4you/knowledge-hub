import { Pipe, PipeTransform, inject } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

/**
 * 把外部 URL 标记为可信资源 URL，绕过 Angular DomSanitizer 的拦截，
 * 供 iframe [src] 等绑定使用。
 *
 * 注意：使用前应当校验 URL 来源，避免开放重定向 / XSS 风险。
 */
@Pipe({ name: 'safeResourceUrl', standalone: true })
export class SafeResourceUrlPipe implements PipeTransform {
  private readonly sanitizer = inject(DomSanitizer);

  transform(url: string | null | undefined): SafeResourceUrl | string {
    if (!url) return '';
    // 简单校验：只允许 http/https 协议
    const trimmed = url.trim();
    if (!/^https?:\/\//i.test(trimmed)) {
      return '';
    }
    return this.sanitizer.bypassSecurityTrustResourceUrl(trimmed);
  }
}