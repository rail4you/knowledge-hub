import { mapEnumToOptions } from '@abp/ng.core';

export enum EmploymentOutcomeStatus {
  Intention = 0,
  Signed = 1,
  Employed = 2,
  FurtherStudy = 3,
  Entrepreneurship = 4,
  Unemployed = 5,
}

export const employmentOutcomeStatusOptions = mapEnumToOptions(EmploymentOutcomeStatus);
