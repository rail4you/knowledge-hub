import { mapEnumToOptions } from '@abp/ng.core';

export enum NewsCommentStatus {
  Approved = 0,
  Hidden = 1,
  Rejected = 2,
}

export const newsCommentStatusOptions = mapEnumToOptions(NewsCommentStatus);
