import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { tap } from 'rxjs/operators';

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
 * - 按 namespace 隔离不同业务，避免相互影响。
 */
@Injectable({ providedIn: 'root' })
export class ClientCacheService {
  private static readonly DEFAULT_TTL_MS = 60_000;
  private static readonly MAX_ENTRIES_PER_NAMESPACE = 50;

  private readonly stores = new Map<string, Map<string, CacheEntry>>();

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
    return loader().pipe(tap(value => this.set(namespace, key, value)));
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
}
