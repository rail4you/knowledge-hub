import { mapEnumToOptions } from '@abp/ng.core';

export enum UserRoleType {
  None = 0,
  LeagueAdmin = 1,
  SchoolAdmin = 2,
  Teacher = 3,
  Student = 4,
  EnterpriseUser = 5,
}

export const userRoleTypeOptions = mapEnumToOptions(UserRoleType);
