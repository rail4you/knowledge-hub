import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { Rest, PagedResultDto } from '@abp/ng.core';
import type { ResourceDto } from '../../proxy/resources/models';

export interface WorkbenchResourceQuery {
  filter?: string;
  /** 只返回该用户创建的资源（老师端「共享资源」候选：不能共享同事的资源） */
  creatorId?: string;
  skipCount: number;
  maxResultCount: number;
}

/**
 * 教学资源库工作台 · 共享候选资源查询。
 *
 * <p>为什么手写：生成的 `proxy/resources/resource.service.ts` 把参数逐个白名单映射，
 * 新增的 <c>creatorId</c> 在重新生成 proxy 之前会被丢掉，导致「只显示我创建的资源」静默失效。</p>
 */
@Injectable({ providedIn: 'root' })
export class WorkbenchShareService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getMyResources = (input: WorkbenchResourceQuery, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceDto>>(
      {
        method: 'GET',
        url: '/api/app/resource/filtered-list',
        params: {
          filter: input.filter,
          creatorId: input.creatorId,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
