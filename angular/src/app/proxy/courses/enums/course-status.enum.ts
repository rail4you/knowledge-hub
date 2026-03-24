import { mapEnumToOptions } from '@abp/ng.core';

export enum CourseStatus {
  Draft = 0,
  PendingReview = 1,
  SchoolApproved = 2,
  LeagueApproved = 3,
  Rejected = 4,
  Published = 5,
}

export const courseStatusOptions = mapEnumToOptions(CourseStatus);
