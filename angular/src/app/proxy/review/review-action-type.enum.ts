import { mapEnumToOptions } from '@abp/ng.core';

export enum ReviewActionType {
  Create = 0,
  Update = 1,
  Delete = 2,
  Admit = 3,
  Publish = 4,
  Certify = 5,
  Revoke = 6,
}

export const reviewActionTypeOptions = mapEnumToOptions(ReviewActionType);
