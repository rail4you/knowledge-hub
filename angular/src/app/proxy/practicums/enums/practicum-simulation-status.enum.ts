import { mapEnumToOptions } from '@abp/ng.core';

export enum PracticumSimulationStatus {
  Processing = 0,
  Ready = 1,
  Invalid = 2,
}

export const practicumSimulationStatusOptions = mapEnumToOptions(PracticumSimulationStatus);
