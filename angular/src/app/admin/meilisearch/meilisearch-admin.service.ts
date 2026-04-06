import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RestService } from '@abp/ng.core';

export interface MeiliHealthDto {
  status: string;
}

export interface MeiliVersionDto {
  commitSha: string;
  commitDate: string;
  pkgVersion: string;
}

export interface MeiliIndexStatsDto {
  numberOfDocuments: number;
  isIndexing: boolean;
  fieldDistribution: Record<string, number>;
}

export interface MeiliStatsDto {
  databaseSize: number;
  usedDatabaseSize: number;
  lastUpdate: string | null;
  indexes: Record<string, MeiliIndexStatsDto>;
}

export interface MeiliIndexDto {
  uid: string;
  primaryKey: string | null;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface MeiliEmbedderDto {
  source: string;
  url: string | null;
  model: string | null;
  dimensions: number | null;
  documentTemplate: string | null;
}

export interface MeiliTaskDto {
  uid: number;
  indexUid: string | null;
  status: string;
  type: string;
  enqueuedAt: string | null;
  startedAt: string | null;
  finishedAt: string | null;
}

export interface MeiliDashboardDto {
  health: MeiliHealthDto;
  version: MeiliVersionDto;
  stats: MeiliStatsDto;
  indexes: MeiliIndexDto[];
  embedders: Record<string, MeiliEmbedderDto>;
  recentTasks: MeiliTaskDto[];
}

export interface MeiliDocumentGroupDto {
  resourceName: string;
  resourceId: string | null;
  fileExtension: string | null;
  pageCount: number;
  pages: MeiliDocumentPageDto[];
  resourceType: 'document' | 'video';
  videoUrl: string | null;
  uploadDate: string | null;
}

export interface MeiliDocumentPageDto {
  id: string;
  pageNumber: number;
  pageTitle: string | null;
  pageContent: string | null;
  eventDescription: string | null;
  startTime: string | null;
  endTime: string | null;
}

export interface PageIndexListItemDto {
  id: string;
  resourceName: string;
  resourceId: string;
  versionNumber: number;
  tenantId: string | null;
  sourceFormat: string | null;
  model: string | null;
  nodeCount: number;
  docDescription: string | null;
  createdAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class MeiliSearchAdminService {
  private readonly restService = inject(RestService);
  private readonly apiUrl = '/api/app/meili-search-admin';

  getDashboard(): Observable<MeiliDashboardDto> {
    return this.restService.request({
      method: 'GET',
      url: `${this.apiUrl}/dashboard`
    }, { apiName: 'Default' });
  }

  getIndexStats(indexUid: string): Observable<MeiliIndexStatsDto> {
    return this.restService.request({
      method: 'GET',
      url: `${this.apiUrl}/index-stats`,
      params: { indexUid }
    }, { apiName: 'Default' });
  }

  getEmbedders(indexUid: string): Observable<Record<string, MeiliEmbedderDto>> {
    return this.restService.request({
      method: 'GET',
      url: `${this.apiUrl}/embedders`,
      params: { indexUid }
    }, { apiName: 'Default' });
  }

  getRecentTasks(limit = 20): Observable<MeiliTaskDto[]> {
    return this.restService.request({
      method: 'GET',
      url: `${this.apiUrl}/recent-tasks`,
      params: { limit }
    }, { apiName: 'Default' });
  }

  getIndexDocuments(indexUid: string, limit = 200): Observable<MeiliDocumentGroupDto[]> {
    return this.restService.request({
      method: 'GET',
      url: `${this.apiUrl}/index-documents`,
      params: { indexUid, limit }
    }, { apiName: 'Default' });
  }

  getIndexes(): Observable<MeiliIndexDto[]> {
    return this.restService.request({
      method: 'GET',
      url: `${this.apiUrl}/indexes`
    }, { apiName: 'Default' });
  }

  getPageIndexList(): Observable<PageIndexListItemDto[]> {
    return this.restService.request({
      method: 'GET',
      url: `${this.apiUrl}/page-index-list`
    }, { apiName: 'Default' });
  }
}
