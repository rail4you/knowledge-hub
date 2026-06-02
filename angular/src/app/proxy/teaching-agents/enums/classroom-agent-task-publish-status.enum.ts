import { mapEnumToOptions } from '@abp/ng.core';

export enum ClassroomAgentTaskPublishStatus {
  Draft = 0,
  Published = 1,
  Closed = 2,
}

export const classroomAgentTaskPublishStatusOptions = mapEnumToOptions(ClassroomAgentTaskPublishStatus);
