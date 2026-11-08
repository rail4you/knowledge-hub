import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { Rest, PagedResultDto } from '@abp/ng.core';

/** 「资源互转」左栏：课程资源行。 */
export interface CourseConvertibleResource {
  resourceId: string;
  name: string;
  fileExtension?: string | null;
  fileSize?: number | null;
  resourceType?: number;
  status?: number;
  /** 0 新建 / 1 来自课程 / 2 来自资源库 */
  sourceType: number;
  sourceName?: string | null;
  creatorName?: string | null;
  creationTime?: string;
  libraryNames: string[];
  /** 该资源已共享进去的资源库 Id（按目标库判断是否重复共享） */
  sharedLibraryIds?: string[];
  /** 是否已共享到任意资源库（仅提示，不禁用） */
  isSharedToLibrary?: boolean;
}

/** 「资源互转」右栏：库资源行。 */
export interface LibraryConvertibleResource {
  resourceId: string;
  name: string;
  fileExtension?: string | null;
  fileSize?: number | null;
  resourceType?: number;
  status?: number;
  sourceType: number;
  sourceName?: string | null;
  creatorName?: string | null;
  creationTime?: string;
  ownerTenantName?: string | null;
}

export interface ResourceLibraryOption {
  id: string;
  name: string;
  visibility: number;
  /** 是否本人可见/可参与的库；false = 仅因本人资源在里面而列出 */
  isMine: boolean;
}

export interface ResourceConversionResult {
  resourceId: string;
  name: string;
  message: string;
}

/**
 * 课程资源 ⇄ 教学资源库资源 互转。
 *
 * <p>平台口径：两类资源分开处理，互转 = 复制一份新记录（带来源标记），
 * 之后两侧各自维护、互不同步。手写服务，参数直接透传。</p>
 */
@Injectable({ providedIn: 'root' })
export class ResourceConversionService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getMyLibraries = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceLibraryOption[]>(
      { method: 'GET', url: '/api/app/resource-conversion/my-libraries' },
      { apiName: this.apiName, ...config },
    );

  getCourseResources = (courseId: string, filter?: string, skipCount = 0, maxResultCount = 50, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CourseConvertibleResource>>(
      {
        method: 'GET',
        url: '/api/app/resource-conversion/course-resources',
        params: { courseId, filter, skipCount, maxResultCount },
      },
      { apiName: this.apiName, ...config },
    );

  getLibraryResources = (libraryId: string, filter?: string, skipCount = 0, maxResultCount = 50, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LibraryConvertibleResource>>(
      {
        method: 'GET',
        url: '/api/app/resource-conversion/library-resources',
        params: { libraryId, filter, skipCount, maxResultCount },
      },
      { apiName: this.apiName, ...config },
    );

  convertToLibrary = (input: { resourceId: string; sourceCourseId?: string; libraryId: string; name?: string }, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceConversionResult>(
      { method: 'POST', url: '/api/app/resource-conversion/convert-to-library', body: input },
      { apiName: this.apiName, ...config },
    );

  convertToCourse = (input: { resourceId: string; libraryId: string; courseId: string; name?: string }, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceConversionResult>(
      { method: 'POST', url: '/api/app/resource-conversion/convert-to-course', body: input },
      { apiName: this.apiName, ...config },
    );
}
