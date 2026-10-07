import { mapEnumToOptions } from '@abp/ng.core';

export enum AiTaskType {
  LessonPlanSingle = 0,
  LessonPlanMulti = 10,
  CaseAnalysis = 20,
  CareerGuidance = 30,
  ExerciseGenerate = 40,
  ImageGeneration = 50,
  VideoGeneration = 60,
}

export const aiTaskTypeOptions = mapEnumToOptions(AiTaskType);
