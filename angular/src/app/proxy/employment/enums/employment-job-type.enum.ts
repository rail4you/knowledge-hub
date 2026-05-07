import { mapEnumToOptions } from '@abp/ng.core';

export enum EmploymentJobType {
  FullTime = 0,
  Internship = 1,
  PartTime = 2,
  Apprenticeship = 3,
}

export const employmentJobTypeOptions = mapEnumToOptions(EmploymentJobType);
