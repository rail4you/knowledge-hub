import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceLibraryLearnerStatus {
  Active = 0,
  Disabled = 1,
}

export const resourceLibraryLearnerStatusOptions = mapEnumToOptions(ResourceLibraryLearnerStatus);
