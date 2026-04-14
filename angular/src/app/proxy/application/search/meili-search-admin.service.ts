import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { MeiliDashboardDto, MeiliDocumentGroupDto, MeiliEmbedderDto, MeiliIndexDto, MeiliIndexStatsDto, MeiliTaskDto, PageIndexListItemDto } from '../contracts/search/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class MeiliSearchAdminService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getDashboard = (tenantId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MeiliDashboardDto>({
      method: 'GET',
      url: '/api/app/meili-search-admin/dashboard',
      params: { tenantId },
    },
    { apiName: this.apiName,...config });
  

  getEmbedders = (indexUid: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Record<string, MeiliEmbedderDto>>({
      method: 'GET',
      url: '/api/app/meili-search-admin/embedders',
      params: { indexUid },
    },
    { apiName: this.apiName,...config });
  

  getIndexDocuments = (indexUid: string, limit: number = 200, tenantId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MeiliDocumentGroupDto[]>({
      method: 'GET',
      url: '/api/app/meili-search-admin/index-documents',
      params: { indexUid, limit, tenantId },
    },
    { apiName: this.apiName,...config });
  

  getIndexStats = (indexUid: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MeiliIndexStatsDto>({
      method: 'GET',
      url: '/api/app/meili-search-admin/index-stats',
      params: { indexUid },
    },
    { apiName: this.apiName,...config });
  

  getIndexes = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, MeiliIndexDto[]>({
      method: 'GET',
      url: '/api/app/meili-search-admin/indexes',
    },
    { apiName: this.apiName,...config });
  

  getPageIndexList = (tenantId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PageIndexListItemDto[]>({
      method: 'GET',
      url: '/api/app/meili-search-admin/page-index-list',
      params: { tenantId },
    },
    { apiName: this.apiName,...config });
  

  getRecentTasks = (limit: number = 20, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MeiliTaskDto[]>({
      method: 'GET',
      url: '/api/app/meili-search-admin/recent-tasks',
      params: { limit },
    },
    { apiName: this.apiName,...config });
}