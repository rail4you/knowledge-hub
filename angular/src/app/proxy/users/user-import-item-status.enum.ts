import { mapEnumToOptions } from '@abp/ng.core';

export enum UserImportItemStatus {
  New = 0,
  Overwrite = 1,
  Skip = 2,
  Fail = 3,
}

export const userImportItemStatusOptions = mapEnumToOptions(UserImportItemStatus);
