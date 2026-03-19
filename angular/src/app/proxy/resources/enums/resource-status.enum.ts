import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceStatus {
  Draft = 0,
  PendingReview = 1,
  SchoolApproved = 2,
  LeagueApproved = 3,
  Rejected = 4,
  Hidden = 5,
}

export const resourceStatusOptions = mapEnumToOptions(ResourceStatus);
