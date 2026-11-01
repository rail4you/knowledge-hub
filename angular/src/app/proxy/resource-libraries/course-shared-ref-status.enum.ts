import { mapEnumToOptions } from '@abp/ng.core';

export enum CourseSharedRefStatus {
  Active = 0,
  Revoked = 1,
}

export const courseSharedRefStatusOptions = mapEnumToOptions(CourseSharedRefStatus);
