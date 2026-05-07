import { mapEnumToOptions } from '@abp/ng.core';

export enum DoubleHighValueSourceType {
  Manual = 0,
  Automatic = 1,
}

export const doubleHighValueSourceTypeOptions = mapEnumToOptions(DoubleHighValueSourceType);
