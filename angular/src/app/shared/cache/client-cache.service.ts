import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { finalize, share, tap } from 'rxjs/operators';

interface CacheEntry {
  value: unknown;
  at: number;
}

/**
 * 通用前端页面级内存缓存（带 TTL，默认 60s）。
 *
 * 用于「读多写少」的公共数据（列表、详情、分类、下拉等），
 * 避免每次进入页面都重新请求，从而消除重复的 loading 闪烁。
 *
 * - 命中缓存时同步返回（of），调用方的 loading 状态不会闪烁；
 * - 写入/更新/删除类操作后，调用方应使用 reload() 或 invalidate() 使缓存失效；
 * - 按 namespace 隔离不同业务，避免相互影响；
 * - clearMany() 会通过 localStorage 广播到同浏览器的其它标签页，
 *   使「后台改数据 → 另一个标签页的学生端」也能立即看到变化。
 */
@Injectable({ providedIn: 'root' })
export class ClientCacheService {
  private static readonly DEFAULT_TTL_MS = 60_000;
  private static readonly MAX_ENTRIES_PER_NAMESPACE = 50;

  /** 跨标签页失效广播用的 localStorage key */
  private static readonly BROADCAST_KEY = 'kh:cache-invalidate';

  private readonly stores = new Map<string, Map<string, CacheEntry>>();

  /** 进行中的请求，按 namespace::key 去重，避免并发重复请求同一 key */
  private readonly inflight = new Map<string, Observable<unknown>>();

  constructor() {
    if (typeof window === 'undefined') return;

    // 监听其它标签页的失效广播（storage 事件只在其它标签页触发，当前页不触发）。
    window.addEventListener('storage', (event: StorageEvent) => {
      if (event.key !== ClientCacheService.BROADCAST_KEY || !event.newValue) return;
      try {
        const payload = JSON.parse(event.newValue) as { namespaces?: unknown };
        if (Array.isArray(payload?.namespaces)) {
          // 收到广播后只清本地，不再回播，避免标签页之间互相触发。
          this.clearLocally(payload.namespaces as string[]);
        }
      } catch {
        // 广播内容损坏时忽略，不影响主流程。
      }
    });
  }

  /** 读取缓存；过期或不存在返回 undefined */
  get<T>(namespace: string, key: string): T | undefined {
    const store = this.stores.get(namespace);
    const entry = store?.get(key);
    if (!entry) return undefined;
    if (Date.now() - entry.at >= ClientCacheService.DEFAULT_TTL_MS) {
      store!.delete(key);
      return undefined;
    }
    return entry.value as T;
  }

  /** 写入缓存 */
  set<T>(namespace: string, key: string, value: T): void {
    let store = this.stores.get(namespace);
    if (!store) {
      store = new Map<string, CacheEntry>();
      this.stores.set(namespace, store);
    }
    // 简单防膨胀：条目过多时整体清空
    if (store.size >= ClientCacheService.MAX_ENTRIES_PER_NAMESPACE) {
      store.clear();
    }
    store.set(key, { value, at: Date.now() });
  }

  /** 命中缓存则同步返回，否则执行 loader 并写入缓存 */
  load<T>(namespace: string, key: string, loader: () => Observable<T>): Observable<T> {
    const cached = this.get<T>(namespace, key);
    if (cached !== undefined) {
      return of(cached);
    }

    const inflightKey = `${namespace}::${key}`;
    const existing = this.inflight.get(inflightKey) as Observable<T> | undefined;
    if (existing) {
      return existing;
    }

    const request$ = loader().pipe(
      tap(value => this.set(namespace, key, value)),
      finalize(() => this.inflight.delete(inflightKey)),
      share()
    );
    this.inflight.set(inflightKey, request$);
    return request$;
  }

  /** 忽略缓存强制加载并写入缓存（用于新增/修改/删除后刷新） */
  reload<T>(namespace: string, key: string, loader: () => Observable<T>): Observable<T> {
    return loader().pipe(tap(value => this.set(namespace, key, value)));
  }

  /** 删除单个缓存项 */
  invalidate(namespace: string, key: string): void {
    this.stores.get(namespace)?.delete(key);
  }

  /** 清空指定 namespace（不传则清空全部） */
  clear(namespace?: string): void {
    if (namespace) {
      this.stores.delete(namespace);
    } else {
      this.stores.clear();
    }
  }

  /**
   * 批量清空多个 namespace（不传则清空全部）。
   * 用于「一次写操作影响多个缓存域」的场景，如后台下架资讯后同时失效
   * 公共资讯列表与学生端资讯列表。
   *
   * 同时通过 localStorage 广播到同浏览器的其它标签页，使跨标签页也能立即刷新。
   */
  clearMany(namespaces: string[]): void {
    if (!namespaces || namespaces.length === 0) {
      this.stores.clear();
      this.broadcast([]);
      return;
    }

    this.clearLocally(namespaces);
    this.broadcast(namespaces);
  }

  /** 仅清本地缓存（收到跨标签页广播时使用，不再回播） */
  private clearLocally(namespaces: string[]): void {
    for (const namespace of namespaces) {
      this.stores.delete(namespace);
    }
  }

  /** 把失效通知广播到同浏览器的其它标签页 */
  private broadcast(namespaces: string[]): void {
    if (typeof localStorage === 'undefined') return;

    try {
      // 带随机后缀，确保连续两次相同内容的失效也能触发 storage 事件。
      localStorage.setItem(
        ClientCacheService.BROADCAST_KEY,
        JSON.stringify({ namespaces, at: Date.now(), n: Math.random() }),
      );
    } catch {
      // 隐私模式/存储已满时忽略广播：本地失效仍然生效。
    }
  }
}
