import { mapEnumToOptions } from '@abp/ng.core';

export enum TenantType {
  Professional = 0,
  Project = 1,
}

export const tenantTypeOptions = mapEnumToOptions(TenantType);
