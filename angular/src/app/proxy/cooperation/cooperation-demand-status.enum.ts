import { mapEnumToOptions } from '@abp/ng.core';

export enum CooperationDemandStatus {
  Draft = 0,
  PendingReview = 1,
  Published = 2,
  Rejected = 3,
  Closed = 4,
  Archived = 5,
}

export const cooperationDemandStatusOptions = mapEnumToOptions(CooperationDemandStatus);
