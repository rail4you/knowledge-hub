import { mapEnumToOptions } from '@abp/ng.core';

export enum AssignmentType {
  Individual = 0,
  Group = 1,
}

export const assignmentTypeOptions = mapEnumToOptions(AssignmentType);
