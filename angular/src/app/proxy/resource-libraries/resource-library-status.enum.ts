import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceLibraryStatus {
  Draft = 0,
  Active = 1,
  Archived = 2,
}

export const resourceLibraryStatusOptions = mapEnumToOptions(ResourceLibraryStatus);
