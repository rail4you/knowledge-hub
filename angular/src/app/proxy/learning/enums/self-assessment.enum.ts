import { mapEnumToOptions } from '@abp/ng.core';

export enum SelfAssessment {
  None = 0,
  Correct = 1,
  PartiallyCorrect = 2,
  Incorrect = 3,
}

export const selfAssessmentOptions = mapEnumToOptions(SelfAssessment);
