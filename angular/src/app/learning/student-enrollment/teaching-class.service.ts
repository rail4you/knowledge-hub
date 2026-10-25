import type { PagedResultDto, Rest } from '@abp/ng.core';
import { RestService } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

/**
 * 教学班前端服务。
 *
 * ⚠️ 临时说明：本服务是对新接口 /api/app/teaching-class/* 的手写封装。
 * 后端接口刚新增，API 进程尚未重启加载，因此代理（proxy/）还没法生成。
 * 待 API 重启后，应执行：
 *   cd angular && abp generate-proxy -t ng
 * 然后用生成的 proxy/courses/teaching-class.service.ts 替换本文件，
 * 并删除本文件（保持「一份数据源」原则）。
 *
 * 路由按 ABP 约定控制器推导，规则以本仓库既有实现为准：
 *   AddMembersAsync(Guid classId, Dto)  → POST   /{classId}/members
 *   RemoveMemberAsync(Guid classId, Guid studentId) → DELETE /{classId}/member/{studentId}
 *   GetMembersAsync(input)             → GET    /members
 *   GetListAsync(input)                → GET    /list
 *   CreateAsync(input) / UpdateAsync(id, input) / DeleteAsync(id) / GetAsync(id) 同常规
 * （对照实现：proxy/resource-libraries/resource-library.service.ts 的
 *   addMember → /{id}/member、removeMember → /{id}/member/{memberTenantId}）
 */

export interface TeachingClassDto {
  id: string;
  tenantId?: string | null;
  name: string;
  majorId?: string | null;
  majorName?: string | null;
  courseId?: string | null;
  courseName?: string | null;
  teacherId?: string | null;
  teacherName?: string | null;
  gradeYear?: number | null;
  description?: string | null;
  /** 0=Draft 1=Active 2=Archived */
  status: number;
  memberCount: number;
  creationTime?: string;
}

export interface TeachingClassMemberDto {
  id: string;
  classId: string;
  studentId: string;
  studentName?: string | null;
  studentNumber?: string | null;
  userName?: string | null;
  majorName?: string | null;
  /** 0=Manual 1=Import 2=SelfJoin */
  source: number;
  joinedAt?: string;
}

export interface GetTeachingClassesInput {
  filter?: string;
  majorId?: string | null;
  courseId?: string | null;
  teacherId?: string | null;
  status?: number | null;
  tenantId?: string | null;
  skipCount: number;
  maxResultCount: number;
  sorting?: string;
}

export interface CreateTeachingClassInput {
  name: string;
  majorId?: string | null;
  courseId?: string | null;
  teacherId?: string | null;
  gradeYear?: number | null;
  description?: string | null;
  status?: number;
}

@Injectable({
  providedIn: 'root',
})
export class TeachingClassService {
  private readonly restService = inject(RestService);

  private readonly base = '/api/app/teaching-class';

  getList = (input: GetTeachingClassesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TeachingClassDto>>(
      {
        method: 'GET',
        url: `${this.base}/list`,
        params: {
          filter: input.filter,
          majorId: input.majorId,
          courseId: input.courseId,
          teacherId: input.teacherId,
          status: input.status,
          tenantId: input.tenantId,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: 'KnowledgeHub', ...config }
    );

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingClassDto>(
      { method: 'GET', url: `${this.base}/${id}` },
      { apiName: 'KnowledgeHub', ...config }
    );

  create = (input: CreateTeachingClassInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingClassDto>(
      { method: 'POST', url: this.base, body: input },
      { apiName: 'KnowledgeHub', ...config }
    );

  update = (id: string, input: CreateTeachingClassInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingClassDto>(
      { method: 'PUT', url: `${this.base}/${id}`, body: input },
      { apiName: 'KnowledgeHub', ...config }
    );

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      { method: 'DELETE', url: `${this.base}/${id}` },
      { apiName: 'KnowledgeHub', ...config }
    );

  /** 批量添加班级成员，返回真正新增的学生 Id（已存在的自动跳过） */
  addMembers = (
    classId: string,
    input: { studentIds: string[]; source?: number },
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, string[]>(
      { method: 'POST', url: `${this.base}/${classId}/members`, body: input },
      { apiName: 'KnowledgeHub', ...config }
    );

  removeMember = (classId: string, studentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      { method: 'DELETE', url: `${this.base}/${classId}/member/${studentId}` },
      { apiName: 'KnowledgeHub', ...config }
    );

  getMembers = (
    input: { classId: string; filter?: string; skipCount: number; maxResultCount: number },
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, PagedResultDto<TeachingClassMemberDto>>(
      {
        method: 'GET',
        url: `${this.base}/members`,
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: 'KnowledgeHub', ...config }
    );
}
