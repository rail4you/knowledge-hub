import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { ConfigStateService } from '@abp/ng.core';

/**
 * Portal API 服务 - 用于学生端获取租户资源库信息
 */
@Injectable({ providedIn: 'root' })
export class PortalService {
  private readonly rest = inject(RestService);
  private readonly http = inject(HttpClient);
  private readonly configState = inject(ConfigStateService);

  private readonly baseUrl = '/api/app/portal';

  /**
   * 获取当前租户的首页数据
   */
  getHomeData(tenantId: string): Observable<PortalHomeDataDto> {
    return this.http.get<PortalHomeDataDto>(`${this.baseUrl}/home-data/${tenantId}`);
  }

  /**
   * 获取所有租户的资源库摘要列表（公开访问）
   * 用于主页展示所有租户
   */
  getPublicTenantList(): Observable<TenantResourceSummaryDto[]> {
    return this.http.get<TenantResourceSummaryDto[]>(`${this.baseUrl}/public-tenant-list`);
  }
}

export interface PortalHomeDataDto {
  tenantInfo: TenantBriefDto;
  stats: PortalStatsDto;
  microMajors: MicroMajorBriefDto[];
  featuredCourses: CourseBriefDto[];
  latestMaterials: MaterialBriefDto[];
  latestNews: NewsBriefDto[];
  partners: PartnerBriefDto[];
}

export interface TenantBriefDto {
  id: string;
  name: string;
  description?: string;
  logoUrl?: string;
  industryField?: string;
}

export interface PortalStatsDto {
  courseCount: number;
  resourceCount: number;
  studentCount: number;
  microMajorCount: number;
}

export interface MicroMajorBriefDto {
  id: string;
  title: string;
  coverImageUrl?: string;
  courseCount: number;
}

export interface CourseBriefDto {
  id: string;
  title: string;
  coverImageUrl?: string;
  teacherName?: string;
  studentCount: number;
}

export interface MaterialBriefDto {
  id: string;
  name: string;
  fileExtension?: string;
  downloadCount: number;
  coverUrl?: string;
}

export interface NewsBriefDto {
  id: string;
  title: string;
  publishedAt?: string;
}

export interface PartnerBriefDto {
  id: string;
  name: string;
}

/**
 * 租户资源库摘要（公开访问）
 */
export interface TenantResourceSummaryDto {
  id: string;
  name: string;
  description?: string;
  logoUrl?: string;
  industryField?: string;
  courseCount: number;
  resourceCount: number;
  microMajorCount: number;
}