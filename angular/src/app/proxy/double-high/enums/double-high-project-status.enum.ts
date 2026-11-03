import { mapEnumToOptions } from '@abp/ng.core';

export enum DoubleHighProjectStatus {
  Draft = 0,
  Active = 1,
  Closed = 2,
  Submitted = 3,
}

export const doubleHighProjectStatusOptions = mapEnumToOptions(DoubleHighProjectStatus);
