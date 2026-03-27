import { mapEnumToOptions } from '@abp/ng.core';

export enum AllianceStatus {
  Active = 0,
  Suspended = 1,
  Dissolved = 2,
}

export const allianceStatusOptions = mapEnumToOptions(AllianceStatus);
