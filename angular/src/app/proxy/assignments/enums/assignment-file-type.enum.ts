import { mapEnumToOptions } from '@abp/ng.core';

export enum AssignmentFileType {
  Requirement = 0,
  Submission = 1,
  Annotated = 2,
}

export const assignmentFileTypeOptions = mapEnumToOptions(AssignmentFileType);
