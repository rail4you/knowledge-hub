import { mapEnumToOptions } from '@abp/ng.core';

export enum DoubleHighObjectType {
  Manual = 0,
  ResourceLibrary = 1,
  Major = 2,
  Course = 3,
}

export const doubleHighObjectTypeOptions = mapEnumToOptions(DoubleHighObjectType);
