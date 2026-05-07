import { mapEnumToOptions } from '@abp/ng.core';

export enum DoubleHighDataSourceType {
  Manual = 0,
  ResourceCount = 1,
  CourseCount = 2,
  MicroMajorCount = 3,
  PracticumProjectCount = 4,
  NewsArticleCount = 5,
  MicroMajorEnrollmentCount = 6,
  PracticumEnrollmentCount = 7,
}

export const doubleHighDataSourceTypeOptions = mapEnumToOptions(DoubleHighDataSourceType);
