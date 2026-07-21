import { Pipe, PipeTransform, inject } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

/**
 * 把外部 URL 标记为可信资源 URL，绕过 Angular DomSanitizer 的拦截，
 * 供 iframe [src] 等绑定使用。
 *
 * 注意：使用前应当校验 URL 来源，避免开放重定向 / XSS 风险。
 *
 * 自动升级：当页面通过 HTTPS 加载且 URL 为 http:// 时，重写为后端 HTTP 代理路径
 * （/api/proxy/http/{host}/{path}），避免浏览器 Mixed Content 策略拦截 iframe。
 */
@Pipe({ name: 'safeResourceUrl', standalone: true })
export class SafeResourceUrlPipe implements PipeTransform {
  private readonly sanitizer = inject(DomSanitizer);
  private readonly isHttps = typeof window !== 'undefined' && window.location.protocol === 'https:';

  transform(url: string | null | undefined): SafeResourceUrl | string {
    if (!url) return '';
    const trimmed = url.trim();
    // 允许 http/https 绝对路径，也允许 / 开头的相对路径（如本地 WASM 镜像 /wasm/xxx/index.html）
    if (!/^(https?:\/\/|\/)/i.test(trimmed)) {
      return '';
    }

    // HTTPS 页面 + HTTP 资源 → 走后端代理，避免 Mixed Content 被拦截
    if (this.isHttps && /^http:\/\//i.test(trimmed)) {
      const proxyUrl = this.buildProxyUrl(trimmed);
      if (proxyUrl) {
        return this.sanitizer.bypassSecurityTrustResourceUrl(proxyUrl);
      }
    }

    return this.sanitizer.bypassSecurityTrustResourceUrl(trimmed);
  }

  /**
   * 将 http://host:port/path?query → /api/proxy/http/host:port/path?query
   * 返回 null 表示 URL 格式不支持代理。
   */
  private buildProxyUrl(httpUrl: string): string | null {
    try {
      const u = new URL(httpUrl);
      const host = u.host; // hostname:port
      const path = u.pathname + u.search + u.hash;
      return `/api/proxy/http/${host}${path}`;
    } catch {
      return null;
    }
  }
}