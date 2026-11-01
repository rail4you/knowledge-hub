import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourcePublicationStatus {
  Draft = 0,
  PendingReview = 1,
  Published = 2,
  Rejected = 3,
  Withdrawn = 4,
}

export const resourcePublicationStatusOptions = mapEnumToOptions(ResourcePublicationStatus);
