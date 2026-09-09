import { mapEnumToOptions } from '@abp/ng.core';

export enum SpecialEduContentType {
  TeachingDesign = 0,
  Iep = 1,
  Resource = 2,
}

export const specialEduContentTypeOptions = mapEnumToOptions(SpecialEduContentType);
