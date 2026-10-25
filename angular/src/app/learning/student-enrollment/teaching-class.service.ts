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
 * 路由以 ABP 约定控制器实际生成的模板为准（已用 /api/abp/api-definition 核对并实测）：
 *   GetListAsync(input)                  → GET    /api/app/teaching-class
 *   GetAsync(id)                         → GET    /api/app/teaching-class/{id}
 *   CreateAsync(input)                   → POST   /api/app/teaching-class
 *   UpdateAsync(id, input)               → PUT    /api/app/teaching-class/{id}
 *   DeleteAsync(id)                      → DELETE /api/app/teaching-class/{id}
 *   AddMembersAsync(Guid classId, Dto)   → POST   /api/app/teaching-class/members/{classId}
 *   RemoveMemberAsync(classId, studentId)→ DELETE /api/app/teaching-class/member?classId=..&studentId=..
 *   GetMembersAsync(input)               → GET    /api/app/teaching-class/members
 *
 * 关键规则：ABP 只有把「名为 id 的参数」放进路径，其余参数名（如 classId/studentId）
 * 走 query 绑定 —— 这也是 resource-library 的 removeMember 形如 /{id}/member/{x}
 * 而本服务形如 /member?classId=.. 的原因。
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
        // ABP 约定控制器：GetListAsync 映射到集合根 GET，不是 /list
        url: this.base,
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
      // ABP 把唯一 Guid 参数提升为路径参数 → /members/{classId}
      { method: 'POST', url: `${this.base}/members/${classId}`, body: input },
      { apiName: 'KnowledgeHub', ...config }
    );

  removeMember = (classId: string, studentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      // 注意：ABP 只把「名为 id 的参数」放进路径；classId/studentId 走 query 绑定，
      // 因此路由是 /member?classId=..&studentId=..（不是 /{classId}/member/{studentId}）
      {
        method: 'DELETE',
        url: `${this.base}/member`,
        params: { classId, studentId },
      },
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
