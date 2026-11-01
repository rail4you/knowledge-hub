import { mapEnumToOptions } from '@abp/ng.core';

export enum CooperationResponseStatus {
  Submitted = 0,
  Accepted = 1,
  Declined = 2,
  Withdrawn = 3,
}

export const cooperationResponseStatusOptions = mapEnumToOptions(CooperationResponseStatus);
