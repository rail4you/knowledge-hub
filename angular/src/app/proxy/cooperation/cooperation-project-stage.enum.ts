import { mapEnumToOptions } from '@abp/ng.core';

export enum CooperationProjectStage {
  Confirming = 0,
  Executing = 1,
  AcceptancePending = 2,
  Archived = 3,
  Terminated = 4,
}

export const cooperationProjectStageOptions = mapEnumToOptions(CooperationProjectStage);
