import { mapEnumToOptions } from '@abp/ng.core';

export enum ClassroomAgentTaskTargetType {
  Course = 0,
  Resource = 1,
  ExerciseSet = 2,
}

export const classroomAgentTaskTargetTypeOptions = mapEnumToOptions(ClassroomAgentTaskTargetType);
