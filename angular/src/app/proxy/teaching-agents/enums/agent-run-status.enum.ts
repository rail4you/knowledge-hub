import { mapEnumToOptions } from '@abp/ng.core';

export enum AgentRunStatus {
  Pending = 0,
  InProgress = 1,
  Completed = 2,
  Failed = 3,
}

export const agentRunStatusOptions = mapEnumToOptions(AgentRunStatus);
