import { mapEnumToOptions } from '@abp/ng.core';

export enum IdeologyUsageMethod {
  Import = 0,
  CaseAnalysis = 1,
  Discussion = 2,
  PracticeComment = 3,
  ExtendedReading = 4,
}

export const ideologyUsageMethodOptions = mapEnumToOptions(IdeologyUsageMethod);
