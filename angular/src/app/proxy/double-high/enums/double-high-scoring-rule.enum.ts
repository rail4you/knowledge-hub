import { mapEnumToOptions } from '@abp/ng.core';

export enum DoubleHighScoringRule {
  Proportional = 0,
  Threshold = 1,
  Direct = 2,
  None = 3,
}

export const doubleHighScoringRuleOptions = mapEnumToOptions(DoubleHighScoringRule);
