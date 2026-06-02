import { mapEnumToOptions } from '@abp/ng.core';

export enum TeachingAgentVisibility {
  Private = 0,
  School = 1,
  Public = 2,
}

export const teachingAgentVisibilityOptions = mapEnumToOptions(TeachingAgentVisibility);
