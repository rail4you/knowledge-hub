import { mapEnumToOptions } from '@abp/ng.core';

export enum IndexingJobStatus {
  Pending = 0,
  Parsing = 10,
  Indexing = 20,
  Completed = 30,
  Failed = 40,
  Cancelled = 50,
}

export const indexingJobStatusOptions = mapEnumToOptions(IndexingJobStatus);
