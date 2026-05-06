import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Observable } from 'rxjs';

export enum MicroMajorStatus {
  Draft = 0,
  Published = 1,
  Archived = 2,
}

export enum MicroMajorEnrollmentStatus {
  Enrolled = 0,
  InProgress = 1,
  Completed = 2,
  Certified = 3,
  Cancelled = 4,
}

export enum MicroMajorCertificateStatus {
  Active = 0,
  Revoked = 1,
}

export interface MicroMajorCourseDto {
  id: string;
  microMajorId: string;
  courseId: string;
  courseTitle?: string;
  courseCoverImageUrl?: string;
  major?: string;
  semester?: string;
  sortOrder: number;
  isCore: boolean;
}

export interface MicroMajorDto {
  id: string;
  title: string;
  summary?: string;
  description?: string;
  coverImageUrl?: string;
  industryField?: string;
  collaborationUnit?: string;
  status: MicroMajorStatus;
  requiredCompletionRate: number;
  isCertificateEnabled: boolean;
  courseCount: number;
  enrollmentCount: number;
  currentUserProgress?: number;
  isCurrentUserEnrolled: boolean;
  creationTime: string;
}

export interface MicroMajorDetailDto extends MicroMajorDto {
  courses: MicroMajorCourseDto[];
}

export interface CreateUpdateMicroMajorCourseDto {
  courseId: string;
  sortOrder: number;
  isCore: boolean;
}

export interface CreateUpdateMicroMajorDto {
  title: string;
  summary?: string;
  description?: string;
  coverImageUrl?: string;
  industryField?: string;
  collaborationUnit?: string;
  status: MicroMajorStatus;
  requiredCompletionRate: number;
  isCertificateEnabled: boolean;
  courses: CreateUpdateMicroMajorCourseDto[];
}

export interface PagedMicroMajorRequestDto {
  filter?: string;
  status?: MicroMajorStatus;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface MicroMajorEnrollmentDto {
  id: string;
  microMajorId: string;
  microMajorTitle?: string;
  studentId: string;
  studentName?: string;
  status: MicroMajorEnrollmentStatus;
  progress: number;
  enrolledAt: string;
  completedAt?: string;
  certificateIssuedAt?: string;
}

export interface GetMicroMajorEnrollmentsInput {
  microMajorId?: string;
  studentId?: string;
  status?: MicroMajorEnrollmentStatus;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface MicroMajorCertificateDto {
  id: string;
  microMajorId: string;
  microMajorTitle?: string;
  enrollmentId: string;
  studentId: string;
  studentName?: string;
  certificateNo: string;
  verifyCode: string;
  status: MicroMajorCertificateStatus;
  issuedAt: string;
}

@Injectable({
  providedIn: 'root',
})
export class MicroMajorService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  get(id: string): Observable<MicroMajorDto> {
    return this.restService.request<any, MicroMajorDto>({
      method: 'GET',
      url: `/api/app/micro-major/${id}`,
    }, { apiName: this.apiName });
  }

  getDetail(id: string): Observable<MicroMajorDetailDto> {
    return this.restService.request<any, MicroMajorDetailDto>({
      method: 'GET',
      url: `/api/app/micro-major/${id}/detail`,
    }, { apiName: this.apiName });
  }

  getList(input: PagedMicroMajorRequestDto): Observable<PagedResultDto<MicroMajorDto>> {
    return this.restService.request<any, PagedResultDto<MicroMajorDto>>({
      method: 'GET',
      url: '/api/app/micro-major',
      params: input,
    }, { apiName: this.apiName });
  }

  getPublished(input: PagedMicroMajorRequestDto): Observable<PagedResultDto<MicroMajorDto>> {
    return this.restService.request<any, PagedResultDto<MicroMajorDto>>({
      method: 'GET',
      url: '/api/app/micro-major/published',
      params: input,
    }, { apiName: this.apiName });
  }

  create(input: CreateUpdateMicroMajorDto): Observable<MicroMajorDto> {
    return this.restService.request<any, MicroMajorDto>({
      method: 'POST',
      url: '/api/app/micro-major',
      body: input,
    }, { apiName: this.apiName });
  }

  update(id: string, input: CreateUpdateMicroMajorDto): Observable<MicroMajorDto> {
    return this.restService.request<any, MicroMajorDto>({
      method: 'PUT',
      url: `/api/app/micro-major/${id}`,
      body: input,
    }, { apiName: this.apiName });
  }

  delete(id: string): Observable<void> {
    return this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/micro-major/${id}`,
    }, { apiName: this.apiName });
  }

  enroll(microMajorId: string): Observable<void> {
    return this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/micro-major/enroll/${microMajorId}`,
    }, { apiName: this.apiName });
  }

  getMyEnrollments(): Observable<MicroMajorEnrollmentDto[]> {
    return this.restService.request<any, MicroMajorEnrollmentDto[]>({
      method: 'GET',
      url: '/api/app/micro-major/my-enrollments',
    }, { apiName: this.apiName });
  }

  getMyCertificates(): Observable<MicroMajorCertificateDto[]> {
    return this.restService.request<any, MicroMajorCertificateDto[]>({
      method: 'GET',
      url: '/api/app/micro-major/my-certificates',
    }, { apiName: this.apiName });
  }

  getEnrollmentList(input: GetMicroMajorEnrollmentsInput): Observable<PagedResultDto<MicroMajorEnrollmentDto>> {
    return this.restService.request<any, PagedResultDto<MicroMajorEnrollmentDto>>({
      method: 'GET',
      url: '/api/app/micro-major/enrollment-list',
      params: input,
    }, { apiName: this.apiName });
  }

  issueCertificate(enrollmentId: string): Observable<MicroMajorCertificateDto> {
    return this.restService.request<any, MicroMajorCertificateDto>({
      method: 'POST',
      url: `/api/app/micro-major/issue-certificate/${enrollmentId}`,
    }, { apiName: this.apiName });
  }
}
