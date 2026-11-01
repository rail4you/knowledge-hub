import { mapEnumToOptions } from '@abp/ng.core';

export enum LibraryTemplateStatus {
  Draft = 0,
  Published = 1,
  Disabled = 2,
}

export const libraryTemplateStatusOptions = mapEnumToOptions(LibraryTemplateStatus);
