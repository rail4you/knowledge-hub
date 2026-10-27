import type { ApplyCertificateInput, ApproveCertificateInput, CertificateApplicationDto, CertificateApplyEligibilityDto, CertificateDto, CertificatePrepareDto, CertificateTemplateDto, CertificateVerifyDto, CreateUpdateCertificateTemplateDto, GetCertificateApplicationsInput, RejectCertificateInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CertificateService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  apply = (input: ApplyCertificateInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CertificateApplicationDto>({
      method: 'POST',
      url: '/api/app/certificate/apply',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  approve = (input: ApproveCertificateInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CertificateDto>({
      method: 'POST',
      url: '/api/app/certificate/approve',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createTemplate = (input: CreateUpdateCertificateTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CertificateTemplateDto>({
      method: 'POST',
      url: '/api/app/certificate/template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deleteTemplate = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/certificate/${id}/template`,
    },
    { apiName: this.apiName,...config });
  

  getApplications = (input: GetCertificateApplicationsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CertificateApplicationDto>>({
      method: 'GET',
      url: '/api/app/certificate/applications',
      params: { status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getApplyEligibility = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CertificateApplyEligibilityDto>({
      method: 'GET',
      url: `/api/app/certificate/apply-eligibility/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getMyApplications = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CertificateApplicationDto[]>({
      method: 'GET',
      url: '/api/app/certificate/my-applications',
    },
    { apiName: this.apiName,...config });
  

  getMyCertificates = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CertificateDto[]>({
      method: 'GET',
      url: '/api/app/certificate/my-certificates',
    },
    { apiName: this.apiName,...config });
  

  getTemplates = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CertificateTemplateDto[]>({
      method: 'GET',
      url: '/api/app/certificate/templates',
    },
    { apiName: this.apiName,...config });
  

  prepare = (applicationId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CertificatePrepareDto>({
      method: 'POST',
      url: `/api/app/certificate/prepare/${applicationId}`,
    },
    { apiName: this.apiName,...config });
  

  reject = (input: RejectCertificateInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/certificate/reject',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateTemplate = (id: string, input: CreateUpdateCertificateTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CertificateTemplateDto>({
      method: 'PUT',
      url: `/api/app/certificate/${id}/template`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  verify = (code: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CertificateVerifyDto>({
      method: 'POST',
      url: '/api/app/certificate/verify',
      params: { code },
    },
    { apiName: this.apiName,...config });
}