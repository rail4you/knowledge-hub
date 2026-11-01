import { mapEnumToOptions } from '@abp/ng.core';

export enum ReviewTargetType {
  Library = 0,
  LibraryPublication = 1,
  BuildTaskDelivery = 2,
  EnterpriseProfile = 3,
  EmploymentJob = 4,
  CooperationDemand = 5,
}

export const reviewTargetTypeOptions = mapEnumToOptions(ReviewTargetType);
