/**
 * Office 文档（PPTX/DOCX/XLSX）转换后 PDF 的持久化预览缓存。
 *
 * 后端已把转换结果缓存到磁盘，`/preview-pdf` 每次返回同一份文件。这里用浏览器
 * Cache Storage 把整份 PDF 持久化：跨页面刷新、新开标签、下次访问都以缓存为主，
 * 直接交给 pdfjs 渲染，免去重复下载（实测整份 PDF 可达 10MB+，下载 10s+）。
 *
 * - 采用「缓存优先」策略：命中缓存直接渲染，不再询问后端转换状态；
 * - 缓存带 TTL（默认 7 天），过期后重新下载，避免资源被替换后长期显示旧内容；
 * - Cache Storage 不可用（非安全上下文等）时退化为会话内内存缓存。
 */
const CACHE_NAME = 'kh-pdf-preview-v1';
const MAX_AGE_MS = 7 * 24 * 60 * 60 * 1000;

/** Cache Storage 不可用时的内存兜底 */
const memoryCache = new Map<string, { buf: ArrayBuffer; at: number }>();

function canUseCacheStorage(): boolean {
  return typeof caches !== 'undefined' && typeof caches.open === 'function';
}

function absolute(url: string): string {
  return url.startsWith('http') ? url : `${location.origin}${url}`;
}

/** 仅读取本地缓存（不发起网络请求）；无缓存或已过期返回 null */
export async function readCachedPdf(url: string): Promise<ArrayBuffer | null> {
  const key = absolute(url);
  const now = Date.now();

  const mem = memoryCache.get(key);
  if (mem && now - mem.at < MAX_AGE_MS) return mem.buf;

  if (!canUseCacheStorage()) return null;
  try {
    const cache = await caches.open(CACHE_NAME);
    const res = await cache.match(key);
    if (!res) return null;
    const cachedAt = Number(res.headers.get('x-cached-at') || 0);
    if (cachedAt && now - cachedAt >= MAX_AGE_MS) {
      await cache.delete(key);
      return null;
    }
    const buf = await res.arrayBuffer();
    memoryCache.set(key, { buf, at: cachedAt || now });
    return buf;
  } catch {
    return null;
  }
}

/**
 * 把已加载的整份 PDF 字节写入持久化缓存（来源可为 pdfjs 的 getData()）。
 * 复制一份再存储，避免调用方继续使用同一 buffer 时被 detach。
 */
export async function storePdfBytes(url: string, bytes: Uint8Array): Promise<void> {
  const key = absolute(url);
  const buf = bytes.slice().buffer;
  memoryCache.set(key, { buf, at: Date.now() });
  if (!canUseCacheStorage()) return;
  try {
    const cache = await caches.open(CACHE_NAME);
    await cache.put(key, new Response(buf, {
      headers: {
        'Content-Type': 'application/pdf',
        'x-cached-at': String(Date.now()),
      },
    }));
  } catch {
    // 配额不足等场景忽略，内存缓存仍可用
  }
}

/** 清空持久化预览缓存（资源更新等场景可调用） */
export async function clearPdfPreviewCache(): Promise<void> {
  memoryCache.clear();
  if (!canUseCacheStorage()) return;
  try {
    await caches.delete(CACHE_NAME);
  } catch {
    // ignore
  }
}
