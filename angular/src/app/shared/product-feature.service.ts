import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map, shareReplay } from 'rxjs/operators';

/** 产品能力开关（后端 GET /api/app/product-feature）。 */
export interface ProductFeatureDto {
  maxTenantCount: number;
  isAllianceEnabled: boolean;
  isTwoLevelApprovalEnabled: boolean;
  isSpecialEducationEnabled: boolean;
}

const DEFAULTS: ProductFeatureDto = {
  maxTenantCount: 1,
  isAllianceEnabled: false,
  isTwoLevelApprovalEnabled: false,
  isSpecialEducationEnabled: false,
};

/**
 * 产品能力开关读取（联盟协同 / 两级审核 / 特教模块 / 租户上限）。
 *
 * <p><b>为什么不用 proxy 生成的 ProductFeatureService？</b>
 * 代理内部走 ABP 的 <c>RestService</c>，它依赖 ABP 核心初始化时装载的
 * <c>environment.apis</c>。而本服务是在 <c>provideAppInitializer</c> 里最早被调用的，
 * 此时 ABP 核心还没初始化，代理会抛
 * <c>Cannot read properties of undefined (reading 'KnowledgeHub')</c> 并中断启动。
 * 因此这里直接用 <c>HttpClient</c> 请求同源地址（与后端同源，无需 environment.apis）。</p>
 *
 * <p>读取失败一律回退为「全部关闭」，绝不阻塞启动、也不影响主流程。</p>
 */
@Injectable({ providedIn: 'root' })
export class ProductFeatureService {
  private readonly http = inject(HttpClient);
  private cache$: Observable<ProductFeatureDto> | null = null;

  /** 读取产品能力开关（带进程内缓存，失败回退默认值）。 */
  get(): Observable<ProductFeatureDto> {
    if (!this.cache$) {
      this.cache$ = this.http.get<ProductFeatureDto>('/api/app/product-feature').pipe(
        map((res) => ({ ...DEFAULTS, ...(res || {}) })),
        catchError(() => of(DEFAULTS)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }

    return this.cache$;
  }
}
