import { mapEnumToOptions } from '@abp/ng.core';

export enum PracticumSubmissionStatus {
  Submitted = 0,
  Returned = 1,
  Reviewed = 2,
}

export const practicumSubmissionStatusOptions = mapEnumToOptions(PracticumSubmissionStatus);
