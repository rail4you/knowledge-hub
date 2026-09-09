import { mapEnumToOptions } from '@abp/ng.core';

export enum AccountValidityStatus {
  Active = 0,
  Expired = 1,
}

export const accountValidityStatusOptions = mapEnumToOptions(AccountValidityStatus);
