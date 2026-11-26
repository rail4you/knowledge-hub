import { mapEnumToOptions } from '@abp/ng.core';

export enum ExamType {
  Quiz = 0,
  ChapterTest = 1,
  Midterm = 2,
  Final = 3,
}

export const examTypeOptions = mapEnumToOptions(ExamType);
