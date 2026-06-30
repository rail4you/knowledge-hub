import type { BatchEnrollDto, CreateStudentCourseDto, GetAvailableStudentsInput, GetStudentCoursesInput, StudentCourseDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { TenantUserDto } from '../application/identity/models';

@Injectable({
  providedIn: 'root',
})
export class StudentCourseService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  batchEnroll = (input: BatchEnrollDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/student-course/batch-enroll',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/student-course/${id}`,
    },
    { apiName: this.apiName,...config });
  

  enrollStudent = (input: CreateStudentCourseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/student-course/enroll-student',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getAvailableStudents = (input: GetAvailableStudentsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TenantUserDto>>({
      method: 'GET',
      url: '/api/app/student-course/available-students',
      params: { courseId: input.courseId, tenantId: input.tenantId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });

  /** P1-10：跨页全选 — 一次性返回当前筛选下所有可加入学生 ID */
  getAllAvailableStudentIds = (input: GetAvailableStudentsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'GET',
      url: '/api/app/student-course/all-available-student-ids',
      params: { courseId: input.courseId, tenantId: input.tenantId, filter: input.filter },
    },
    { apiName: this.apiName,...config });
  

  getPaged = (input: GetStudentCoursesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StudentCourseDto>>({
      method: 'GET',
      url: '/api/app/student-course/paged',
      params: { courseId: input.courseId, studentId: input.studentId, status: input.status, tenantId: input.tenantId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}