import { mapEnumToOptions } from '@abp/ng.core';

export enum ClassroomTokenKind {
  CheckIn = 0,
  GroupTask = 1,
  Practice = 2,
}

export const classroomTokenKindOptions = mapEnumToOptions(ClassroomTokenKind);
