import { mapEnumToOptions } from '@abp/ng.core';

export enum ReviewStage {
  School = 0,
  League = 1,
}

export const reviewStageOptions = mapEnumToOptions(ReviewStage);
