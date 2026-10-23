import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceLibraryMemberRole {
  Owner = 0,
  Partner = 1,
}

export const resourceLibraryMemberRoleOptions = mapEnumToOptions(ResourceLibraryMemberRole);
