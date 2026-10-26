import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceLibraryVisibility {
  Private = 0,
  Public = 1,
  CoBuilt = 2,
}

export const resourceLibraryVisibilityOptions = mapEnumToOptions(ResourceLibraryVisibility);
