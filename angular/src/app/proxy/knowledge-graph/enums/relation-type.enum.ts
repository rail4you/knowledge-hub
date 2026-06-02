import { mapEnumToOptions } from '@abp/ng.core';

export enum RelationType {
  Prerequisite = 0,
  Corequisite = 1,
  Related = 2,
  Contains = 3,
  Parallel = 4,
  References = 5,
}

export const relationTypeOptions = mapEnumToOptions(RelationType);
