import { mapEnumToOptions } from '@abp/ng.core';

export enum LiveType {
  Recruitment = 0,
  Course = 1,
}

export const liveTypeOptions = mapEnumToOptions(LiveType);
