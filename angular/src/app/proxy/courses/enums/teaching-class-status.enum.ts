import { mapEnumToOptions } from '@abp/ng.core';

export enum TeachingClassStatus {
  Draft = 0,
  Active = 1,
  Archived = 2,
}

export const teachingClassStatusOptions = mapEnumToOptions(TeachingClassStatus);
