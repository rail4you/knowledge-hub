import { mapEnumToOptions } from '@abp/ng.core';

export enum MicroMajorStatus {
  Draft = 0,
  Published = 1,
  Archived = 2,
}

export const microMajorStatusOptions = mapEnumToOptions(MicroMajorStatus);
