import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceLibrarySharingScope {
  Unrestricted = 0,
  SchoolOnly = 1,
  CoBuildMembersOnly = 2,
}

export const resourceLibrarySharingScopeOptions = mapEnumToOptions(ResourceLibrarySharingScope);
