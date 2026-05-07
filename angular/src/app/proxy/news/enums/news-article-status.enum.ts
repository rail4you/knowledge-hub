import { mapEnumToOptions } from '@abp/ng.core';

export enum NewsArticleStatus {
  Draft = 0,
  PendingReview = 1,
  Published = 2,
  Rejected = 3,
  Archived = 4,
}

export const newsArticleStatusOptions = mapEnumToOptions(NewsArticleStatus);
