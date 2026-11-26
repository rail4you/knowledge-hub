import { mapEnumToOptions } from '@abp/ng.core';

export enum SubmissionStatus {
  Draft = 0,
  Submitted = 1,
  Graded = 2,
}

export const submissionStatusOptions = mapEnumToOptions(SubmissionStatus);
