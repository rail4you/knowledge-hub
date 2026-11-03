import { mapEnumToOptions } from '@abp/ng.core';

export enum DoubleHighIndicatorType {
  Auto = 0,
  Number = 1,
  Text = 2,
  EvidenceOnly = 3,
}

export const doubleHighIndicatorTypeOptions = mapEnumToOptions(DoubleHighIndicatorType);
