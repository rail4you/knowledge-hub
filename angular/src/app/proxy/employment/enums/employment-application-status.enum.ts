import { mapEnumToOptions } from '@abp/ng.core';

export enum EmploymentApplicationStatus {
  Submitted = 0,
  Viewed = 1,
  InterviewScheduled = 2,
  Offered = 3,
  Rejected = 4,
  Withdrawn = 5,
}

export const employmentApplicationStatusOptions = mapEnumToOptions(EmploymentApplicationStatus);
