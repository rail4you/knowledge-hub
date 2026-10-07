import { mapEnumToOptions } from '@abp/ng.core';

export enum TeachingSceneCategory {
  Image = 0,
  VideoScene = 10,
  VideoMotion = 20,
}

export const teachingSceneCategoryOptions = mapEnumToOptions(TeachingSceneCategory);
