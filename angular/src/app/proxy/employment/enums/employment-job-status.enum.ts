import { mapEnumToOptions } from '@abp/ng.core';

export enum EmploymentJobStatus {
  Draft = 0,
  PendingReview = 1,
  Published = 2,
  Rejected = 3,
  Closed = 4,
}

export const employmentJobStatusOptions = mapEnumToOptions(EmploymentJobStatus);
