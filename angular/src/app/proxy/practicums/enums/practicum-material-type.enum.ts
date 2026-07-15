import { mapEnumToOptions } from '@abp/ng.core';

export enum PracticumMaterialType {
  Guide = 0,
  Case = 1,
  Template = 2,
  Link = 3,
  /** 仿真实训：嵌入第三方仿真页面（如 Unity WebGL） */
  Simulation = 4,
}

export const practicumMaterialTypeOptions = mapEnumToOptions(PracticumMaterialType);
