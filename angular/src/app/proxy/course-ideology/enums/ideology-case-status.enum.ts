import { mapEnumToOptions } from '@abp/ng.core';

export enum IdeologyCaseStatus {
  Draft = 0,
  Published = 10,
  Archived = 20,
}

export const ideologyCaseStatusOptions = mapEnumToOptions(IdeologyCaseStatus);
