import { mapEnumToOptions } from '@abp/ng.core';

export enum AssignmentStatus {
  Draft = 0,
  Published = 1,
  Closed = 2,
}

export const assignmentStatusOptions = mapEnumToOptions(AssignmentStatus);
