import { mapEnumToOptions } from '@abp/ng.core';

export enum LibraryContentType {
  Resource = 0,
  Course = 1,
  Practicum = 2,
}

export const libraryContentTypeOptions = mapEnumToOptions(LibraryContentType);
