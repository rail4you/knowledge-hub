import { mapEnumToOptions } from '@abp/ng.core';

export enum PracticumEnrollmentStatus {
  Enrolled = 0,
  InProgress = 1,
  Submitted = 2,
  Reviewed = 3,
  Completed = 4,
  Cancelled = 5,
}

export const practicumEnrollmentStatusOptions = mapEnumToOptions(PracticumEnrollmentStatus);
