import { Injectable, inject } from '@angular/core';
import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import type { CourseDto } from '../../proxy/courses/dtos/models';

/** 公共课程查询条件（与后端 PagedCourseRequestDto 对齐的常用字段）。 */
export interface PublicCourseQuery {
  filter?: string;
  majorId?: string;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

/**
 * 学生端 · 公共课程。
 *
 * <para>
 * 公共课程 = 公共资源库（Visibility=Public 且状态有效的教学资源库）里的**已发布**课程。
 * 这些课程可能归属别的学校，但对**所有租户的学生**可见、可选课、可学习，
 * 因此学生端单列一个「公共课程」区域，并注明来源学校（`ownerTenantName`）。
 * </para>
 *
 * <para>
 * 手工服务（不走 proxy 生成）：接口 `GET /api/app/course/public-library-courses`
 * 是在 `ICourseAppService` 上新增的方法，等下次整体重生成 Angular proxy 后
 * 可以换成 proxy 里的强类型方法。
 * </para>
 */
@Injectable({ providedIn: 'root' })
export class PublicCourseService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getList = (input: PublicCourseQuery, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CourseDto>>(
      {
        method: 'GET',
        url: '/api/app/course/public-library-courses',
        params: {
          filter: input.filter,
          majorId: input.majorId,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
