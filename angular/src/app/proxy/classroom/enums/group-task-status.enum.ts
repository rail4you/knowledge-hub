import { mapEnumToOptions } from '@abp/ng.core';

export enum GroupTaskStatus {
  Draft = 0,
  Published = 1,
  Closed = 2,
}

export const groupTaskStatusOptions = mapEnumToOptions(GroupTaskStatus);
