import { mapEnumToOptions } from '@abp/ng.core';

export enum AiTaskStatus {
  Pending = 0,
  Running = 10,
  Completed = 30,
  Failed = 40,
  Cancelled = 50,
}

export const aiTaskStatusOptions = mapEnumToOptions(AiTaskStatus);
