import { mapEnumToOptions } from '@abp/ng.core';

export enum CheckInSessionStatus {
  Active = 0,
  Closed = 1,
}

export const checkInSessionStatusOptions = mapEnumToOptions(CheckInSessionStatus);
