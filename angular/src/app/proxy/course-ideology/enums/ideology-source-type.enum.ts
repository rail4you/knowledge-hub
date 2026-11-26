import { mapEnumToOptions } from '@abp/ng.core';

export enum IdeologySourceType {
  Textbook = 0,
  AuthorityMedia = 1,
  Law = 2,
  Enterprise = 3,
  AiGenerated = 4,
  Other = 5,
}

export const ideologySourceTypeOptions = mapEnumToOptions(IdeologySourceType);
