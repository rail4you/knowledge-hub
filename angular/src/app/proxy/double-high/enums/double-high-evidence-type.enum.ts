import { mapEnumToOptions } from '@abp/ng.core';

export enum DoubleHighEvidenceType {
  ResourceLink = 0,
  AttachmentLink = 1,
  ExternalLink = 2,
}

export const doubleHighEvidenceTypeOptions = mapEnumToOptions(DoubleHighEvidenceType);
