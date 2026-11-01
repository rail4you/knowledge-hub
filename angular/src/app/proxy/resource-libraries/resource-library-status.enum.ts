import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceLibraryStatus {
  Draft = 0,
  Active = 1,
  Archived = 2,
  PendingReview = 3,
  PendingLeagueReview = 4,
  Rejected = 5,
  ChangePending = 6,
  DeletePending = 7,
}

export const resourceLibraryStatusOptions = mapEnumToOptions(ResourceLibraryStatus);
