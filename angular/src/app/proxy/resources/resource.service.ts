import type { AuditResourceDto, CompleteUploadDto, CompleteUploadResultDto, CreatePhysicalDeleteRequestDto, CreateUpdateResourceCategoryDto, CreateUpdateResourceDto, InitiateUploadDto, InitiateUploadResultDto, MeiliSearchQueryDto, MeiliSearchResultDto, PhysicalDeleteRequestDto, ResourceAuditDto, ResourceCategoryDto, ResourceDto, ResourceListQueryDto, ResourceSearchQueryDto, ResourceVersionDto, UploadChunkDto, UploadVersionDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ResourceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  approvePhysicalDelete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PhysicalDeleteRequestDto>({
      method: 'POST',
      url: `/api/app/resource/${id}/approve-physical-delete`,
    },
    { apiName: this.apiName,...config });
  

  audit = (input: AuditResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceAuditDto>({
      method: 'POST',
      url: '/api/app/resource/audit',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  cancelUpload = (uploadId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: `/api/app/resource/cancel-upload/${uploadId}`,
    },
    { apiName: this.apiName,...config });
  

  collect = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/resource/collect/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  completeUpload = (input: CompleteUploadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompleteUploadResultDto>({
      method: 'POST',
      url: '/api/app/resource/complete-upload',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceDto>({
      method: 'POST',
      url: '/api/app/resource',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createCategory = (input: CreateUpdateResourceCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceCategoryDto>({
      method: 'POST',
      url: '/api/app/resource/category',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteCategory = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/resource/${id}/category`,
    },
    { apiName: this.apiName,...config });
  

  download = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      url: `/api/app/resource/download/${resourceId}`,
      responseType: 'blob',
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceDto>({
      method: 'GET',
      url: `/api/app/resource/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAudits = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceAuditDto[]>({
      method: 'GET',
      url: `/api/app/resource/audits/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getCategories = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceCategoryDto[]>({
      method: 'GET',
      url: '/api/app/resource/categories',
    },
    { apiName: this.apiName,...config });
  

  getFileUrl = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'GET',
      responseType: 'text',
      url: `/api/app/resource/file-url/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getFilteredList = (input: ResourceListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceDto>>({
      method: 'GET',
      url: '/api/app/resource/filtered-list',
      params: { filter: input.filter, status: input.status, resourceType: input.resourceType, categoryId: input.categoryId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceDto>>({
      method: 'GET',
      url: '/api/app/resource',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPendingAuditList = (input: ResourceListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceDto>>({
      method: 'GET',
      url: '/api/app/resource/pending-audit-list',
      params: { filter: input.filter, status: input.status, resourceType: input.resourceType, categoryId: input.categoryId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPendingPhysicalDeleteRequests = (input: ResourceListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PhysicalDeleteRequestDto>>({
      method: 'GET',
      url: '/api/app/resource/pending-physical-delete-requests',
      params: { filter: input.filter, status: input.status, resourceType: input.resourceType, categoryId: input.categoryId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getVersions = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceVersionDto[]>({
      method: 'GET',
      url: `/api/app/resource/versions/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  getWithVersions = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceDto>({
      method: 'GET',
      url: `/api/app/resource/${id}/with-versions`,
    },
    { apiName: this.apiName,...config });
  

  initiateUpload = (input: InitiateUploadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InitiateUploadResultDto>({
      method: 'POST',
      url: '/api/app/resource/initiate-upload',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  isCollected = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: `/api/app/resource/is-collected/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  rejectPhysicalDelete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PhysicalDeleteRequestDto>({
      method: 'POST',
      url: `/api/app/resource/${id}/reject-physical-delete`,
    },
    { apiName: this.apiName,...config });
  

  requestPhysicalDelete = (input: CreatePhysicalDeleteRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PhysicalDeleteRequestDto>({
      method: 'POST',
      url: '/api/app/resource/request-physical-delete',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  rollbackVersion = (versionId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceVersionDto>({
      method: 'POST',
      url: `/api/app/resource/rollback-version/${versionId}`,
    },
    { apiName: this.apiName,...config });
  

  search = (input: ResourceSearchQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceDto>>({
      method: 'POST',
      url: '/api/app/resource/search',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  searchDocuments = (input: MeiliSearchQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MeiliSearchResultDto>({
      method: 'POST',
      url: '/api/app/resource/search-documents',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  seedTestDocuments = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/resource/seed-test-documents',
    },
    { apiName: this.apiName,...config });
  

  submitForReview = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/resource/submit-for-review/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  uncollect = (resourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/resource/uncollect/${resourceId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateResourceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceDto>({
      method: 'PUT',
      url: `/api/app/resource/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateCategory = (id: string, input: CreateUpdateResourceCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceCategoryDto>({
      method: 'PUT',
      url: `/api/app/resource/${id}/category`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  uploadChunk = (input: UploadChunkDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: '/api/app/resource/upload-chunk',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  uploadVersion = (input: UploadVersionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ResourceVersionDto>({
      method: 'POST',
      url: '/api/app/resource/upload-version',
      body: input,
    },
    { apiName: this.apiName,...config });
}