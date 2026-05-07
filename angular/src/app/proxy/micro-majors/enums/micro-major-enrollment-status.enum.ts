import { mapEnumToOptions } from '@abp/ng.core';

export enum MicroMajorEnrollmentStatus {
  Enrolled = 0,
  InProgress = 1,
  Completed = 2,
  Certified = 3,
  Cancelled = 4,
}

export const microMajorEnrollmentStatusOptions = mapEnumToOptions(MicroMajorEnrollmentStatus);
