import { mapEnumToOptions } from '@abp/ng.core';

export enum StudentRecommendationType {
  WeakKnowledge = 0,
  Prerequisite = 1,
  IncompleteChapter = 2,
  LearningHabit = 3,
}

export const studentRecommendationTypeOptions = mapEnumToOptions(StudentRecommendationType);
