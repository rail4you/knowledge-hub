import { Pipe, PipeTransform } from '@angular/core';

/**
 * 把外部 sourceUrl 改写到本地镜像 publicUrl（命中时），
 * 未命中则原样返回，方便上游 safeResourceUrl 走 /api/proxy/http/... 兜底。
 *
 * 该 pipe 是**纯函数**：不做 HTTP、不读缓存。
 * readyMap 由组件层（StudentPracticumDetailComponent 等）一次性拉取并写入 signal。
 *
 * @example
 *   <iframe [src]="sim.resourceUrl | wasmMirrorUrl : wasmMirrorMap() | safeResourceUrl" />
 */
@Pipe({ name: 'wasmMirrorUrl', standalone: true, pure: true })
export class WasmMirrorUrlPipe implements PipeTransform {
  transform(url: string | null | undefined, readyMap: Map<string, string> | null | undefined): string {
    if (!url) return '';
    if (!readyMap || readyMap.size === 0) return url;
    const normalized = normalizeSource(url);
    return readyMap.get(normalized) ?? url;
  }
}

/**
 * 规范化 sourceUrl 作为映射 key：
 *   - 去除末尾斜杠
 *   - 去除 query / fragment
 *   - host + scheme 小写
 *
 * 与后端 WasmMirrorAppService.NormalizeSourceUrl 保持一致。
 */
export function normalizeSource(url: string): string {
  if (!url) return '';
  const trimmed = url.trim();
  try {
    const u = new URL(trimmed);
    const path = u.pathname.replace(/\/+$/, '');
    const portPart = (u.port && !isDefaultPort(u.protocol, u.port)) ? `:${u.port}` : '';
    return `${u.protocol}//${u.hostname.toLowerCase()}${portPart}${path}`.toLowerCase();
  } catch {
    // 非绝对 URL：仅去除末尾斜杠
    return trimmed.replace(/\/+$/, '').toLowerCase();
  }
}

function isDefaultPort(protocol: string, port: string): boolean {
  if (!port) return true;
  if (protocol === 'http:' && port === '80') return true;
  if (protocol === 'https:' && port === '443') return true;
  return false;
}
