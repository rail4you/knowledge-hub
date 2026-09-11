import type { AiTaskType } from '../../../ai/ai-task-type.enum';
import type { AiTaskStatus } from '../../../ai/ai-task-status.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AiGenerationTaskDto {
  id?: string;
  creatorUserId?: string;
  creatorUserName?: string | null;
  taskType?: AiTaskType;
  status?: AiTaskStatus;
  title?: string;
  resourceId?: string | null;
  resourceName?: string | null;
  progress?: number;
  progressMessage?: string | null;
  errorMessage?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  retryCount?: number;
  isRead?: boolean;
  creationTime?: string;
  inputJson?: string | null;
  resultJson?: string | null;
}

export interface ChatMessageDto {
  id?: string;
  role?: string;
  content?: string;
  createdAt?: string;
}

export interface ChatThreadDto {
  id?: string;
  title?: string | null;
  resourceId?: string | null;
  resourceName?: string | null;
  messageCount?: number;
  lastMessage?: string | null;
  createdAt?: string;
  messages?: ChatMessageDto[];
}

export interface CreateAiGenerationTaskDto {
  taskType?: AiTaskType;
  title?: string;
  resourceId?: string | null;
  resourceName?: string | null;
  inputJson?: string;
}

export interface GetAiGenerationTaskListDto extends PagedAndSortedResultRequestDto {
  taskType?: AiTaskType | null;
  status?: AiTaskStatus | null;
  filter?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  onlyMine?: boolean;
}

export interface ResourceForChatDto {
  id?: string;
  name?: string;
  fileExtension?: string | null;
  sourceFormat?: string | null;
  nodeCount?: number;
  hasPageIndex?: boolean;
  hasSummary?: boolean;
  categoryId?: string | null;
  categoryName?: string | null;
}
