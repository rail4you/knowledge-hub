import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { Rest, PagedResultDto } from '@abp/ng.core';
import type { CourseDto } from '../../proxy/courses/dtos/models';

/**
 * 课程管理（教师端 `/learning/course-list`）专用查询参数。
 *
 * <p>为什么要手写而不是用 `proxy/courses/course.service`：
 * 生成的 proxy 会把参数逐个白名单映射（`params: { filter: input.filter, ... }`），
 * 新增的 <c>onlyMine</c> 在重新生成 proxy 之前会被丢掉，导致「只看我创建的课程」静默失效。
 * 这里直接走 `RestService`，把 <c>onlyMine</c> 明确传给后端。</p>
 */
export interface ManagedCourseQuery {
  /** 课程名称关键字 */
  filter?: string;
  /** 主专业筛选 */
  majorId?: string;
  /** 只返回公共课（无专业归属） */
  onlyPublicCourses?: boolean;
  /** 按教学资源库筛选 */
  libraryId?: string;
  /** 只返回「我创建的课程」（TeacherId / CreatorId 命中当前用户） */
  onlyMine?: boolean;
  /** 按租户筛选（Host 上下文切租户时用） */
  tenantId?: string;
  skipCount: number;
  maxResultCount: number;
}

/** 课程管理列表（教师端）：/api/app/course */
@Injectable({ providedIn: 'root' })
export class ManagedCourseService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getList = (input: ManagedCourseQuery, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CourseDto>>(
      {
        method: 'GET',
        url: '/api/app/course',
        params: {
          filter: input.filter,
          majorId: input.majorId,
          onlyPublicCourses: input.onlyPublicCourses,
          libraryId: input.libraryId,
          onlyMine: input.onlyMine,
          tenantId: input.tenantId,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
