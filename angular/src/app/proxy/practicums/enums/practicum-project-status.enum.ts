import { mapEnumToOptions } from '@abp/ng.core';

export enum PracticumProjectStatus {
  Draft = 0,
  Published = 1,
  Archived = 2,
}

export const practicumProjectStatusOptions = mapEnumToOptions(PracticumProjectStatus);
