import { mapEnumToOptions } from '@abp/ng.core';

export enum CourseLessonPlanSource {
  Upload = 0,
  Ai = 1,
}

export const courseLessonPlanSourceOptions = mapEnumToOptions(CourseLessonPlanSource);
