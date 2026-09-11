import { mapEnumToOptions } from '@abp/ng.core';

export enum ResourceArtifactState {
  Ready = 0,
  Failed = 40,
}

export const resourceArtifactStateOptions = mapEnumToOptions(ResourceArtifactState);
