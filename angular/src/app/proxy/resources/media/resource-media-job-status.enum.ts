import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceMediaJobStatus {
  Pending = 0,
  Running = 10,
  Completed = 30,
  PartialFailed = 35,
  Failed = 40,
  Cancelled = 50,
}

export const resourceMediaJobStatusOptions = mapEnumToOptions(ResourceMediaJobStatus);
