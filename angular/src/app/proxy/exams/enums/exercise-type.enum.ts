import { mapEnumToOptions } from '@abp/ng.core';

export enum ExerciseType {
  SingleChoice = 0,
  MultiChoice = 1,
  TrueFalse = 2,
  FillBlank = 3,
  ShortAnswer = 4,
  Essay = 5,
  CaseAnalysis = 6,
  TermExplanation = 7,
  Calculation = 8,
  Programming = 9,
}

export const exerciseTypeOptions = mapEnumToOptions(ExerciseType);
