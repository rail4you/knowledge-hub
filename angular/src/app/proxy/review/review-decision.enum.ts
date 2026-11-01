import { mapEnumToOptions } from '@abp/ng.core';

export enum ReviewDecision {
  Approved = 0,
  Rejected = 1,
  Returned = 2,
}

export const reviewDecisionOptions = mapEnumToOptions(ReviewDecision);
