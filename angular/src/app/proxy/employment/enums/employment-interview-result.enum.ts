import { mapEnumToOptions } from '@abp/ng.core';

export enum EmploymentInterviewResult {
  Pending = 0,
  Passed = 1,
  Deferred = 2,
  Failed = 3,
}

export const employmentInterviewResultOptions = mapEnumToOptions(EmploymentInterviewResult);
