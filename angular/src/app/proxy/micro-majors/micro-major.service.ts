import type { CreateUpdateMicroMajorDto, GetMicroMajorEnrollmentsInput, MicroMajorCertificateDto, MicroMajorDetailDto, MicroMajorDto, MicroMajorEnrollmentDto, PagedMicroMajorRequestDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MicroMajorService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateUpdateMicroMajorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MicroMajorDto>({
      method: 'POST',
      url: '/api/app/micro-major',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/micro-major/${id}`,
    },
    { apiName: this.apiName,...config });
  

  enroll = (microMajorId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/micro-major/enroll/${microMajorId}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MicroMajorDto>({
      method: 'GET',
      url: `/api/app/micro-major/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getDetail = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MicroMajorDetailDto>({
      method: 'GET',
      url: `/api/app/micro-major/${id}/detail`,
    },
    { apiName: this.apiName,...config });
  

  getEnrollmentList = (input: GetMicroMajorEnrollmentsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MicroMajorEnrollmentDto>>({
      method: 'GET',
      url: '/api/app/micro-major/enrollment-list',
      params: { microMajorId: input.microMajorId, studentId: input.studentId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedMicroMajorRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MicroMajorDto>>({
      method: 'GET',
      url: '/api/app/micro-major',
      params: { filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyCertificates = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, MicroMajorCertificateDto[]>({
      method: 'GET',
      url: '/api/app/micro-major/my-certificates',
    },
    { apiName: this.apiName,...config });
  

  getMyEnrollments = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, MicroMajorEnrollmentDto[]>({
      method: 'GET',
      url: '/api/app/micro-major/my-enrollments',
    },
    { apiName: this.apiName,...config });
  

  getPublished = (input: PagedMicroMajorRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MicroMajorDto>>({
      method: 'GET',
      url: '/api/app/micro-major/published',
      params: { filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  issueCertificate = (enrollmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MicroMajorCertificateDto>({
      method: 'POST',
      url: `/api/app/micro-major/issue-certificate/${enrollmentId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateMicroMajorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MicroMajorDto>({
      method: 'PUT',
      url: `/api/app/micro-major/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}