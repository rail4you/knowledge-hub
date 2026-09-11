import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceArtifactKind {
  Thumbnail = 0,
  PreviewPdf = 10,
}

export const resourceArtifactKindOptions = mapEnumToOptions(ResourceArtifactKind);
