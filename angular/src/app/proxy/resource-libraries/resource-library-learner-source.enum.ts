import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceLibraryLearnerSource {
  SelfLearner = 0,
  ClassStudent = 1,
}

export const resourceLibraryLearnerSourceOptions = mapEnumToOptions(ResourceLibraryLearnerSource);
