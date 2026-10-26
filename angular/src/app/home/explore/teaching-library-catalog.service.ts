import { Injectable, inject } from '@angular/core';
import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';

/** 教学资源库目录卡片（后端 TeachingLibraryCardDto）。 */
export interface TeachingLibraryCard {
  id: string;
  name: string;
  description?: string | null;
  coverUrl?: string | null;
  primaryMajorId?: string | null;
  primaryMajorName?: string | null;
  ownerTenantId: string;
  ownerTenantName?: string | null;
  ownerSchoolLogoUrl?: string | null;
  principal?: string | null;
  /** 0 私有 / 1 公开 / 2 院校共建 */
  visibility: number;
  visibilityName?: string | null;
  memberCount: number;
  partnerNames: string[];
  courseCount: number;
  resourceCount: number;
  /** 0 校内 / 1 公共共享 */
  scope: number;
}

export interface TeachingLibraryCatalogQuery {
  /** school 校内（本校主建）/ public 公共共享（他校公开或共建） */
  scope: 'school' | 'public';
  filter?: string;
  primaryMajorId?: string;
  skipCount: number;
  maxResultCount: number;
}

/**
 * 公开门户「教学资源库」目录。
 *
 * <para>
 * 原型 <c>#courses</c> 已从「课程列表」改为「资源库列表」：
 * 校内资源库（本校主建的私有 / 公开 / 共建库）与公共共享资源库（他校公开或共建的库）
 * 分两个分区展示。手工服务，不依赖 proxy 生成。
 * </para>
 */
@Injectable({ providedIn: 'root' })
export class TeachingLibraryCatalogService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getList = (input: TeachingLibraryCatalogQuery, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TeachingLibraryCard>>(
      {
        method: 'GET',
        url: '/api/app/teaching-library-catalog',
        params: {
          scope: input.scope,
          filter: input.filter,
          primaryMajorId: input.primaryMajorId,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
