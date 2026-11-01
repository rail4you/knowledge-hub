import { mapEnumToOptions } from '@abp/ng.core';

export enum EnterpriseCertificationStatus {
  Draft = 0,
  PendingReview = 1,
  Certified = 2,
  Rejected = 3,
  Suspended = 4,
}

export const enterpriseCertificationStatusOptions = mapEnumToOptions(EnterpriseCertificationStatus);
