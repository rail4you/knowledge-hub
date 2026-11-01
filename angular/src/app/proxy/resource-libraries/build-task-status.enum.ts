import { mapEnumToOptions } from '@abp/ng.core';

export enum BuildTaskStatus {
  Draft = 0,
  Published = 1,
  InProgress = 2,
  Submitted = 3,
  Accepted = 4,
  Returned = 5,
  Closed = 6,
}

export const buildTaskStatusOptions = mapEnumToOptions(BuildTaskStatus);
