import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceSourceType {
  Created = 0,
  FromCourse = 1,
  FromLibrary = 2,
}

export const resourceSourceTypeOptions = mapEnumToOptions(ResourceSourceType);
