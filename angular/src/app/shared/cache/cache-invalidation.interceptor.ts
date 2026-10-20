import { HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ClientCacheService } from './client-cache.service';
import { resolveCacheNamespaces } from './cache-domains';

/**
 * 写操作后自动失效学生端缓存的拦截器。
 *
 * 背景：学生端各页面用 ClientCacheService 做 60s 内存缓存（列表/热门/详情），
 * 后台下架/上架/发布资讯后，同一浏览器里切到学生端仍会看到旧数据，
 * 必须等 60s TTL 自然过期才刷新——后台「下架后学生端立刻看不到」这个预期
 * 因此经常对不上。
 *
 * 做法：拦截所有非 GET 的写请求，成功（2xx）后按 URL 命中的业务域
 * 失效对应缓存命名空间（见 cache-domains.ts）。集中在一处实现，
 * 后台新增写操作无需再手动补失效调用。
 *
 * 边界：
 * - 只处理 2xx，失败不失效（避免把旧数据清掉后又拿到同样旧的数据）。
 * - 缓存是浏览器内存级，只对当前浏览器生效；其它设备最长仍需等 60s TTL。
 * - 只读性质的 POST（如点赞、标记已读）也会触发失效，代价是一次多余刷新，
 *   换取「不漏失效」的正确性。
 */
export const cacheInvalidationInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> => {
  // 只处理写操作；GET 不改数据，无需失效。
  if (req.method === 'GET' || req.method === 'HEAD' || req.method === 'OPTIONS') {
    return next(req);
  }

  // 必须在顶层同步 inject——tap 回调里已失去注入上下文。
  const cache = inject(ClientCacheService);
  const namespaces = resolveCacheNamespaces(req.url);

  if (namespaces.length === 0) {
    return next(req);
  }

  return next(req).pipe(
    tap((event) => {
      // 只有 HttpResponse 代表服务端已处理成功；上传进度等事件忽略。
      if (event instanceof HttpResponse && event.status >= 200 && event.status < 300) {
        cache.clearMany(namespaces);
      }
    }),
  );
};
