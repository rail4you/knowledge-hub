import { mapEnumToOptions } from '@abp/ng.core';

export enum StudentCourseStatus {
  Enrolled = 0,
  InProgress = 1,
  Completed = 2,
  Dropped = 3,
}

export const studentCourseStatusOptions = mapEnumToOptions(StudentCourseStatus);
