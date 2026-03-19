import { mapEnumToOptions } from '@abp/ng.core';

export enum AuditType {
  Initial = 0,
  Final = 1,
}

export const auditTypeOptions = mapEnumToOptions(AuditType);
