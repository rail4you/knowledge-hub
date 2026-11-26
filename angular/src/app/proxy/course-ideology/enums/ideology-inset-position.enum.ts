import { mapEnumToOptions } from '@abp/ng.core';

export enum IdeologyInsetPosition {
  Import = 0,
  Lecture = 1,
  Practice = 2,
  Summary = 3,
  Homework = 4,
}

export const ideologyInsetPositionOptions = mapEnumToOptions(IdeologyInsetPosition);
