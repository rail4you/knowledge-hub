import { RestService } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export type TrackingTaskStatus = 'pending' | 'running' | 'success' | 'partial' | 'failed' | 'cancelled';
export type TrackingStepStatus = 'pending' | 'running' | 'success' | 'failed' | 'skipped';

export interface TrackingArtifactDto {
  kind: string;
  variant: string;
  state: string;
  errorMessage?: string;
  sizeBytes?: number;
  generatedAt: string;
}

export interface TrackingTaskDto {
  id: string;
  kind: string;
  title: string;
  status: TrackingTaskStatus;
  progress: number;
  message?: string;
  errorMessage?: string;
  retryCount: number;
  startedAt?: string;
  completedAt?: string;
  creationTime: string;
  resourceVersionId?: string;
  canRetry: boolean;
  artifacts: TrackingArtifactDto[];
}

export interface TrackingStepDto {
  key: string;
  title: string;
  description?: string;
  status: TrackingStepStatus;
  time?: string;
  progress?: number;
  errorMessage?: string;
  tasks: TrackingTaskDto[];
}

export interface ResourceTrackingTimelineDto {
  resourceId: string;
  resourceName: string;
  resourceType: number;
  fileExtension?: string;
  status: number;
  mediaStatus: number;
  currentVersion: number;
  tenantId?: string;
  tenantName?: string;
  visibleToStudents: boolean;
  creationTime: string;
  steps: TrackingStepDto[];
}

export interface ResourceTrackingResourceDto {
  id: string;
  name: string;
  resourceType: number;
  fileExtension?: string;
  status: number;
  mediaStatus: number;
  currentVersion: number;
  creationTime: string;
  lastModificationTime?: string;
  tenantId?: string;
  tenantName?: string;
}

export interface TrackingPagedResult<T> {
  totalCount: number;
  items: T[];
}

export interface GetTrackingResourcesInput {
  filter?: string;
  status?: number;
  skipCount?: number;
  maxResultCount?: number;
}

/**
 * 资源全链路任务跟踪（手写服务，调用 /api/app/resource-tracking）。
 * 聚合媒体任务、索引任务与两级审核记录；重试统一在「资源任务」页进行。
 */
@Injectable({ providedIn: 'root' })
export class ResourceTrackingService {
  private readonly restService = inject(RestService);
  private readonly apiUrl = '/api/app/resource-tracking';

  getResources(input: GetTrackingResourcesInput): Observable<TrackingPagedResult<ResourceTrackingResourceDto>> {
    return this.restService.request<any, TrackingPagedResult<ResourceTrackingResourceDto>>(
      {
        method: 'GET',
        url: this.apiUrl,
        params: {
          filter: input.filter,
          status: input.status,
          skipCount: input.skipCount ?? 0,
          maxResultCount: input.maxResultCount ?? 50,
        },
      },
      { apiName: 'Default' },
    );
  }

  getTimeline(resourceId: string): Observable<ResourceTrackingTimelineDto> {
    return this.restService.request<any, ResourceTrackingTimelineDto>(
      { method: 'GET', url: `${this.apiUrl}/timeline/${resourceId}` },
      { apiName: 'Default' },
    );
  }
}
