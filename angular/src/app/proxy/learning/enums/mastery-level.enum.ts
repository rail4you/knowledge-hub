import { mapEnumToOptions } from '@abp/ng.core';

export enum MasteryLevel {
  Unlearned = 0,
  Learning = 1,
  Mastered = 2,
}

export const masteryLevelOptions = mapEnumToOptions(MasteryLevel);
