import { mapEnumToOptions } from '@abp/ng.core';

export enum ReviewTicketStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
  Cancelled = 3,
}

export const reviewTicketStatusOptions = mapEnumToOptions(ReviewTicketStatus);
