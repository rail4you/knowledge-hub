import { mapEnumToOptions } from '@abp/ng.core';

export enum BuildTaskDeliveryStatus {
  Submitted = 0,
  Accepted = 1,
  Returned = 2,
}

export const buildTaskDeliveryStatusOptions = mapEnumToOptions(BuildTaskDeliveryStatus);
