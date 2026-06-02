import { mapEnumToOptions } from '@abp/ng.core';

export enum TeachingAgentStatus {
  Draft = 0,
  Published = 1,
  Disabled = 2,
}

export const teachingAgentStatusOptions = mapEnumToOptions(TeachingAgentStatus);
