import type { CreateUpdateResourceDto, CreateUpdateResourceCategoryDto, InitiateUploadDto, InitiateUploadResultDto, ResourceAuditDto, ResourceCategoryDto, ResourceDto, ResourceListQueryDto, ResourceVersionDto, UploadChunkDto, UploadVersionDto, CompleteUploadDto, CompleteUploadResultDto, AuditResourceDto, PhysicalDeleteRequestDto, CreatePhysicalDeleteRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ResourceService {
  private restService = inject(RestService);
  apiName = 'Default';

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceDto>({
      method: 'GET',
      url: `/api/app/resource/${id}`,
    },
    { apiName: this.apiName, ...config });

  getFilteredList = (input: ResourceListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceDto>>({
      method: 'GET',
      url: '/api/app/resource/filtered-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount, filter: input.filter, status: input.status, resourceType: input.resourceType, categoryId: input.categoryId },
    },
    { apiName: this.apiName, ...config });

  getWithVersions = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceDto>({
      method: 'GET',
      url: `/api/app/resource/${id}/with-versions`,
    },
    { apiName: this.apiName, ...config });

  create = (input: CreateUpdateResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceDto>({
      method: 'POST',
      url: '/api/app/resource',
      body: input,
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceDto>({
      method: 'PUT',
      url: `/api/app/resource/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource/${id}`,
    },
    { apiName: this.apiName, ...config });

  getVersions = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceVersionDto[]>({
      method: 'GET',
      url: `/api/app/resource/${resourceId}/versions`,
    },
    { apiName: this.apiName, ...config });

  uploadVersion = (input: UploadVersionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceVersionDto>({
      method: 'POST',
      url: '/api/app/resource/upload-version',
      body: input,
    },
    { apiName: this.apiName, ...config });

  rollbackVersion = (versionId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceVersionDto>({
      method: 'POST',
      url: `/api/app/resource/rollback-version/${versionId}`,
    },
    { apiName: this.apiName, ...config });

  isCollected = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'GET',
      url: `/api/app/resource/${resourceId}/is-collected`,
    },
    { apiName: this.apiName, ...config });

  collect = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/resource/${resourceId}/collect`,
    },
    { apiName: this.apiName, ...config });

  uncollect = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource/${resourceId}/collect`,
    },
    { apiName: this.apiName, ...config });

  getCategories = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceCategoryDto[]>({
      method: 'GET',
      url: '/api/app/resource/categories',
    },
    { apiName: this.apiName, ...config });

  createCategory = (input: CreateUpdateResourceCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceCategoryDto>({
      method: 'POST',
      url: '/api/app/resource/categories',
      body: input,
    },
    { apiName: this.apiName, ...config });

  updateCategory = (id: string, input: CreateUpdateResourceCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceCategoryDto>({
      method: 'PUT',
      url: `/api/app/resource/categories/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });

  deleteCategory = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource/categories/${id}`,
    },
    { apiName: this.apiName, ...config });

  getFileUrl = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'GET',
      url: `/api/app/resource/${resourceId}/file-url`,
    },
    { apiName: this.apiName, ...config });

  download = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      url: `/api/app/resource/${resourceId}/download`,
    },
    { apiName: this.apiName, ...config });

  initiateUpload = (input: InitiateUploadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InitiateUploadResultDto>({
      method: 'POST',
      url: '/api/app/chunk-upload/initiate',
      body: input,
    },
    { apiName: this.apiName, ...config });

  uploadChunk = (formData: FormData, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: '/api/app/chunk-upload/upload',
      body: formData,
    },
    { apiName: this.apiName, ...config });

  completeUpload = (input: CompleteUploadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompleteUploadResultDto>({
      method: 'POST',
      url: '/api/app/chunk-upload/complete',
      body: input,
    },
    { apiName: this.apiName, ...config });

  getPendingAuditList = (input: ResourceListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceDto>>({
      method: 'GET',
      url: '/api/app/resource/pending-audit-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount, filter: input.filter },
    },
    { apiName: this.apiName, ...config });

  audit = (input: AuditResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceAuditDto>({
      method: 'POST',
      url: '/api/app/resource/audit',
      body: input,
    },
    { apiName: this.apiName, ...config });

  getAudits = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceAuditDto[]>({
      method: 'GET',
      url: `/api/app/resource/${resourceId}/audits`,
    },
    { apiName: this.apiName, ...config });

  submitForReview = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/resource/${resourceId}/submit-review`,
    },
    { apiName: this.apiName, ...config });

  requestPhysicalDelete = (input: CreatePhysicalDeleteRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PhysicalDeleteRequestDto>({
      method: 'POST',
      url: '/api/app/resource/request-physical-delete',
      body: input,
    },
    { apiName: this.apiName, ...config });

  getPendingPhysicalDeleteRequests = (input: ResourceListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PhysicalDeleteRequestDto>>({
      method: 'GET',
      url: '/api/app/resource/pending-physical-delete-requests',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  approvePhysicalDelete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PhysicalDeleteRequestDto>({
      method: 'POST',
      url: `/api/app/resource/approve-physical-delete/${id}`,
    },
    { apiName: this.apiName, ...config });

  rejectPhysicalDelete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PhysicalDeleteRequestDto>({
      method: 'POST',
      url: `/api/app/resource/reject-physical-delete/${id}`,
    },
    { apiName: this.apiName, ...config });
}
