import { mapEnumToOptions } from '@abp/ng.core';

export enum MicroMajorCertificateStatus {
  Active = 0,
  Revoked = 1,
}

export const microMajorCertificateStatusOptions = mapEnumToOptions(MicroMajorCertificateStatus);
