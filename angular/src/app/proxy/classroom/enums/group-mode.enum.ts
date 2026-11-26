import { mapEnumToOptions } from '@abp/ng.core';

export enum GroupMode {
  TeacherAssigned = 0,
  RandomAuto = 1,
}

export const groupModeOptions = mapEnumToOptions(GroupMode);
