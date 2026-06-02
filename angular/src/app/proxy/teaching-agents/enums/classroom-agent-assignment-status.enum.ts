import { mapEnumToOptions } from '@abp/ng.core';

export enum ClassroomAgentAssignmentStatus {
  Pending = 0,
  InProgress = 1,
  Submitted = 2,
  NeedsTeacherHelp = 3,
}

export const classroomAgentAssignmentStatusOptions = mapEnumToOptions(ClassroomAgentAssignmentStatus);
