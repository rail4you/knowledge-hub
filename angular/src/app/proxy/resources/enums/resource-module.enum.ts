import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceModule {
  None = 0,
  Course = 1,
  Vocational = 2,
  Inclusive = 3,
  Policy = 4,
  LectureHall = 5,
  Textbook = 6,
  TeachingAid = 7,
  ParentClass = 8,
  StudentGrowth = 9,
  Thematic = 10,
}

export const resourceModuleOptions = mapEnumToOptions(ResourceModule);
