import { mapEnumToOptions } from '@abp/ng.core';

export enum ExamStatus {
  NotStarted = 0,
  InProgress = 1,
  Submitted = 2,
  Graded = 3,
}

export const examStatusOptions = mapEnumToOptions(ExamStatus);
