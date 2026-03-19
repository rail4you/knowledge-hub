import { mapEnumToOptions } from '@abp/ng.core';

export enum AuditStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
}

export const auditStatusOptions = mapEnumToOptions(AuditStatus);
