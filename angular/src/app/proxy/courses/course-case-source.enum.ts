import { mapEnumToOptions } from '@abp/ng.core';

export enum CourseCaseSource {
  Manual = 0,
  Import = 1,
  Ai = 2,
}

export const courseCaseSourceOptions = mapEnumToOptions(CourseCaseSource);
