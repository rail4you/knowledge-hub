import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';

/** 与后端 KnowledgeHub.AI.AiTaskType 对齐 */
export enum AiTaskType {
  LessonPlanSingle = 0,
  LessonPlanMulti = 10,
  CaseAnalysis = 20,
  CareerGuidance = 30,
  ExerciseGenerate = 40,
}

/** 与后端 KnowledgeHub.AI.AiTaskStatus 对齐 */
export enum AiTaskStatus {
  Pending = 0,
  Running = 10,
  Completed = 30,
  Failed = 40,
  Cancelled = 50,
}

export interface AiGenerationTaskDto {
  id: string;
  creatorUserId: string;
  creatorUserName?: string;
  taskType: AiTaskType;
  status: AiTaskStatus;
  title: string;
  resourceId?: string;
  resourceName?: string;
  progress: number;
  progressMessage?: string;
  errorMessage?: string;
  startedAt?: string;
  completedAt?: string;
  retryCount: number;
  isRead: boolean;
  creationTime: string;
  inputJson?: string;
  resultJson?: string;
}

export interface CreateAiGenerationTaskDto {
  taskType: AiTaskType;
  title: string;
  resourceId?: string;
  resourceName?: string;
  inputJson: string;
}

export interface GetAiGenerationTaskListDto {
  taskType?: AiTaskType;
  status?: AiTaskStatus;
  filter?: string;
  startTime?: string;
  endTime?: string;
  onlyMine?: boolean;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

@Injectable({ providedIn: 'root' })
export class AiTaskService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  create = (input: CreateAiGenerationTaskDto) =>
    this.restService.request<any, AiGenerationTaskDto>(
      {
        method: 'POST',
        url: '/api/app/ai-generation-task',
        body: input,
      },
      { apiName: this.apiName },
    );

  getList = (input: GetAiGenerationTaskListDto = {}) => {
    const params: Record<string, any> = {
      skipCount: input.skipCount ?? 0,
      maxResultCount: input.maxResultCount ?? 10,
    };
    if (input.taskType !== undefined && input.taskType !== null) params.taskType = input.taskType;
    if (input.status !== undefined && input.status !== null) params.status = input.status;
    if (input.filter) params.filter = input.filter;
    if (input.startTime) params.startTime = input.startTime;
    if (input.endTime) params.endTime = input.endTime;
    if (input.onlyMine !== undefined) params.onlyMine = input.onlyMine;
    if (input.sorting) params.sorting = input.sorting;

    return this.restService.request<any, PagedResultDto<AiGenerationTaskDto>>(
      {
        method: 'GET',
        url: '/api/app/ai-generation-task',
        params,
      },
      { apiName: this.apiName },
    );
  };

  get = (id: string) =>
    this.restService.request<any, AiGenerationTaskDto>(
      {
        method: 'GET',
        url: `/api/app/ai-generation-task/${id}`,
      },
      { apiName: this.apiName },
    );

  getResult = (id: string) =>
    this.restService.request<any, string | null>(
      {
        method: 'GET',
        url: `/api/app/ai-generation-task/${id}/result`,
      },
      { apiName: this.apiName },
    );

  cancel = (id: string) =>
    this.restService.request<any, void>(
      {
        method: 'POST',
        url: `/api/app/ai-generation-task/${id}/cancel`,
      },
      { apiName: this.apiName },
    );

  retry = (id: string) =>
    this.restService.request<any, void>(
      {
        method: 'POST',
        url: `/api/app/ai-generation-task/${id}/retry`,
      },
      { apiName: this.apiName },
    );

  delete = (id: string) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: `/api/app/ai-generation-task/${id}`,
      },
      { apiName: this.apiName },
    );

  getMyUnreadCount = () =>
    this.restService.request<any, number>(
      {
        method: 'GET',
        url: '/api/app/ai-generation-task/my-unread-count',
      },
      { apiName: this.apiName },
    );

  getMyCompleted = (unreadOnly = false) =>
    this.restService.request<any, AiGenerationTaskDto[]>(
      {
        method: 'GET',
        url: '/api/app/ai-generation-task/my-completed',
        params: { unreadOnly },
      },
      { apiName: this.apiName },
    );

  markAsRead = (id: string) =>
    this.restService.request<any, void>(
      {
        method: 'POST',
        url: `/api/app/ai-generation-task/${id}/mark-as-read`,
      },
      { apiName: this.apiName },
    );

  markAllAsRead = () =>
    this.restService.request<any, void>(
      {
        method: 'POST',
        url: '/api/app/ai-generation-task/mark-all-as-read',
      },
      { apiName: this.apiName },
    );

  // ── helpers ──
  static typeLabel(type: AiTaskType): string {
    switch (type) {
      case AiTaskType.LessonPlanSingle: return '教案生成';
      case AiTaskType.LessonPlanMulti: return '多章节教案';
      case AiTaskType.CaseAnalysis: return '案例分析';
      case AiTaskType.CareerGuidance: return '职业规划';
      case AiTaskType.ExerciseGenerate: return '习题生成';
      default: return 'AI 任务';
    }
  }

  static typeColor(type: AiTaskType): string {
    switch (type) {
      case AiTaskType.LessonPlanSingle:
      case AiTaskType.LessonPlanMulti: return 'blue';
      case AiTaskType.CaseAnalysis: return 'purple';
      case AiTaskType.CareerGuidance: return 'cyan';
      case AiTaskType.ExerciseGenerate: return 'orange';
      default: return 'default';
    }
  }

  static statusLabel(status: AiTaskStatus): string {
    switch (status) {
      case AiTaskStatus.Pending: return '排队中';
      case AiTaskStatus.Running: return '生成中';
      case AiTaskStatus.Completed: return '已完成';
      case AiTaskStatus.Failed: return '失败';
      case AiTaskStatus.Cancelled: return '已取消';
      default: return '未知';
    }
  }

  static statusColor(status: AiTaskStatus): string {
    switch (status) {
      case AiTaskStatus.Pending: return 'default';
      case AiTaskStatus.Running: return 'processing';
      case AiTaskStatus.Completed: return 'success';
      case AiTaskStatus.Failed: return 'error';
      case AiTaskStatus.Cancelled: return 'warning';
      default: return 'default';
    }
  }
}
