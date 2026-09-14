import { RestService } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export type TaskStatus = 'pending' | 'running' | 'success' | 'partial' | 'failed' | 'cancelled';

export interface TaskArtifactDto {
  kind?: number;
  variant?: string;
  filePath?: string;
  state?: number;
  errorMessage?: string | null;
  sizeBytes?: number | null;
  generatedAt?: string;
}

export interface ResourceTaskDto {
  id: string;
  resourceId: string;
  /** media | document-index | video-index */
  kind: string;
  status: TaskStatus;
  statusValue: number;
  progress: number;
  message?: string | null;
  errorMessage?: string | null;
  retryCount: number;
  startedAt?: string | null;
  completedAt?: string | null;
  creationTime: string;
  resourceVersionId?: string | null;
  canRetry: boolean;
  artifacts: TaskArtifactDto[];
}

export interface ResourceTaskGroupDto {
  resourceId: string;
  resourceName?: string | null;
  tenantId?: string | null;
  resourceStatus: number;
  mediaStatus: number;
  visibleToStudents: boolean;
  taskCount: number;
  failedCount: number;
  latestTaskStatus?: TaskStatus | null;
  latestCreationTime: string;
  tasks: ResourceTaskDto[];
}

export interface GetResourceTaskGroupsInput {
  filter?: string;
  resourceId?: string;
  resourceStatus?: number;
  taskStatus?: string;
  skipCount?: number;
  maxResultCount?: number;
}

export interface TaskPagedResult<T> {
  totalCount: number;
  items: T[];
}

/**
 * 资源任务（媒体处理 + 文档索引 + 视频索引，按资源聚合）手写服务。
 */
@Injectable({ providedIn: 'root' })
export class ResourceTaskService {
  private readonly restService = inject(RestService);
  private readonly apiUrl = '/api/app/resource-task';

  getGroups(input: GetResourceTaskGroupsInput): Observable<TaskPagedResult<ResourceTaskGroupDto>> {
    return this.restService.request<any, TaskPagedResult<ResourceTaskGroupDto>>(
      {
        method: 'GET',
        url: `${this.apiUrl}/grouped-list`,
        params: {
          filter: input.filter,
          resourceId: input.resourceId,
          resourceStatus: input.resourceStatus,
          taskStatus: input.taskStatus,
          skipCount: input.skipCount ?? 0,
          maxResultCount: input.maxResultCount ?? 10,
        },
      },
      { apiName: 'Default' },
    );
  }

  retry(input: { resourceId: string; kind: string; taskId: string }): Observable<void> {
    return this.restService.request<any, void>(
      { method: 'POST', url: `${this.apiUrl}/retry`, body: input },
      { apiName: 'Default' },
    );
  }
}
