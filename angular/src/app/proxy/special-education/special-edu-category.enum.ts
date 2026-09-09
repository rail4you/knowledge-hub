import { mapEnumToOptions } from '@abp/ng.core';

export enum SpecialEduCategory {
  Peizhi = 0,
  TingZhang = 1,
  ShiZhang = 2,
  Guzuzheng = 3,
}

export const specialEduCategoryOptions = mapEnumToOptions(SpecialEduCategory);
