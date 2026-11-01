import { mapEnumToOptions } from '@abp/ng.core';

export enum ReviewOperation {
  Submit = 0,
  Approve = 1,
  Reject = 2,
  Return = 3,
  Withdraw = 4,
}

export const reviewOperationOptions = mapEnumToOptions(ReviewOperation);
