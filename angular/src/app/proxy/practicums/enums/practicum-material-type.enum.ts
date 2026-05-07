import { mapEnumToOptions } from '@abp/ng.core';

export enum PracticumMaterialType {
  Guide = 0,
  Case = 1,
  Template = 2,
  Link = 3,
}

export const practicumMaterialTypeOptions = mapEnumToOptions(PracticumMaterialType);
