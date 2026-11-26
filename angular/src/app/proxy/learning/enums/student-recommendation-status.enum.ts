import { mapEnumToOptions } from '@abp/ng.core';

export enum StudentRecommendationStatus {
  Pending = 0,
  Viewed = 1,
  InProgress = 2,
  Completed = 3,
  Dismissed = 4,
}

export const studentRecommendationStatusOptions = mapEnumToOptions(StudentRecommendationStatus);
