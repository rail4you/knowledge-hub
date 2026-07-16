import { Pipe, PipeTransform, inject } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

/**
 * 把外部 URL 标记为可信资源 URL，绕过 Angular DomSanitizer 的拦截，
 * 供 iframe [src] 等绑定使用。
 *
 * 注意：使用前应当校验 URL 来源，避免开放重定向 / XSS 风险。
 *
 * 自动升级：当页面通过 HTTPS 加载时，自动将 http:// 替换为 https://，
 * 避免浏览器因 Mixed Content 策略拦截 iframe 内容。
 */
@Pipe({ name: 'safeResourceUrl', standalone: true })
export class SafeResourceUrlPipe implements PipeTransform {
  private readonly sanitizer = inject(DomSanitizer);
  private readonly isHttps = typeof window !== 'undefined' && window.location.protocol === 'https:';

  transform(url: string | null | undefined): SafeResourceUrl | string {
    if (!url) return '';
    // 简单校验：只允许 http/https 协议
    let trimmed = url.trim();
    if (!/^https?:\/\//i.test(trimmed)) {
      return '';
    }
    // HTTPS 页面自动升级 HTTP → HTTPS，避免被浏览器 Mixed Content 拦截
    if (this.isHttps && trimmed.startsWith('http://')) {
      trimmed = 'https://' + trimmed.substring(7);
    }
    return this.sanitizer.bypassSecurityTrustResourceUrl(trimmed);
  }
}