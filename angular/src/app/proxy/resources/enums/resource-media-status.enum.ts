import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceMediaStatus {
  None = 0,
  Processing = 10,
  Ready = 30,
  PartialFailed = 35,
  Failed = 40,
}

export const resourceMediaStatusOptions = mapEnumToOptions(ResourceMediaStatus);
