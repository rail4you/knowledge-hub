import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceType {
  Document = 0,
  Video = 1,
  Audio = 2,
  Image = 3,
  PPT = 4,
}

export const resourceTypeOptions = mapEnumToOptions(ResourceType);
