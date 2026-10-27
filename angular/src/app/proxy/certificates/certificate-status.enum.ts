import { mapEnumToOptions } from '@abp/ng.core';

export enum CertificateStatus {
  Active = 0,
  Revoked = 1,
}

export const certificateStatusOptions = mapEnumToOptions(CertificateStatus);
