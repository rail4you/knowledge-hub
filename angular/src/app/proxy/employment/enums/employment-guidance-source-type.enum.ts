import { mapEnumToOptions } from '@abp/ng.core';

export enum EmploymentGuidanceSourceType {
  Manual = 0,
  AI = 1,
}

export const employmentGuidanceSourceTypeOptions = mapEnumToOptions(EmploymentGuidanceSourceType);
