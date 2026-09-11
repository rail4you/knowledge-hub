/**
 * 通用「字节流」持久化缓存（浏览器 Cache Storage + 会话内存兜底）。
 *
 * 用于把一次性下载的二进制内容（转换后的 PDF、Office/图片预览文件）持久化，
 * 重复打开/刷新/新标签直接命中本地缓存，避免重复下载（部分文件可达 10MB+）。
 *
 * - 带 TTL，过期后重新下载，避免源文件被替换后长期显示旧内容；
 * - Cache Storage 不可用（非安全上下文等）时退化为会话内内存缓存。
 */
export interface BytesCache {
  /** 仅读本地缓存（不发网络请求）；无缓存或过期返回 null */
  read(url: string): Promise<ArrayBuffer | null>;
  /** 写入缓存（自动复制，调用方后续使用原 buffer 不受影响） */
  store(url: string, bytes: Uint8Array): Promise<void>;
  /** 清空缓存 */
  clear(): Promise<void>;
}

const DEFAULT_MAX_AGE_MS = 7 * 24 * 60 * 60 * 1000;

export function createBytesCache(cacheName: string, maxAgeMs = DEFAULT_MAX_AGE_MS): BytesCache {
  const memory = new Map<string, { buf: ArrayBuffer; at: number }>();

  const canUseCacheStorage = (): boolean =>
    typeof caches !== 'undefined' && typeof caches.open === 'function';

  const absolute = (url: string): string =>
    url.startsWith('http') ? url : `${location.origin}${url}`;

  return {
    async read(url: string): Promise<ArrayBuffer | null> {
      const key = absolute(url);
      const now = Date.now();

      const mem = memory.get(key);
      if (mem && now - mem.at < maxAgeMs) return mem.buf;

      if (!canUseCacheStorage()) return null;
      try {
        const cache = await caches.open(cacheName);
        const res = await cache.match(key);
        if (!res) return null;
        const cachedAt = Number(res.headers.get('x-cached-at') || 0);
        if (cachedAt && now - cachedAt >= maxAgeMs) {
          await cache.delete(key);
          return null;
        }
        const buf = await res.arrayBuffer();
        memory.set(key, { buf, at: cachedAt || now });
        return buf;
      } catch {
        return null;
      }
    },

    async store(url: string, bytes: Uint8Array): Promise<void> {
      const key = absolute(url);
      const buf = bytes.slice().buffer;
      memory.set(key, { buf, at: Date.now() });
      if (!canUseCacheStorage()) return;
      try {
        const cache = await caches.open(cacheName);
        await cache.put(key, new Response(buf, {
          headers: {
            'Content-Type': 'application/octet-stream',
            'x-cached-at': String(Date.now()),
          },
        }));
      } catch {
        // 配额不足等场景忽略，内存缓存仍可用
      }
    },

    async clear(): Promise<void> {
      memory.clear();
      if (!canUseCacheStorage()) return;
      try {
        await caches.delete(cacheName);
      } catch {
        // ignore
      }
    },
  };
}
