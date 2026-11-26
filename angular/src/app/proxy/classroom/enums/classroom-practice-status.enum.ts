import { mapEnumToOptions } from '@abp/ng.core';

export enum ClassroomPracticeStatus {
  Active = 0,
  Closed = 1,
}

export const classroomPracticeStatusOptions = mapEnumToOptions(ClassroomPracticeStatus);
