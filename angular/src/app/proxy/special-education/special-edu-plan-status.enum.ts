import { mapEnumToOptions } from '@abp/ng.core';

export enum SpecialEduPlanStatus {
  Draft = 0,
  PendingReview = 1,
  Reviewed = 2,
  Published = 3,
  Archived = 4,
}

export const specialEduPlanStatusOptions = mapEnumToOptions(SpecialEduPlanStatus);
