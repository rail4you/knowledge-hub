import { mapEnumToOptions } from '@abp/ng.core';

export enum CooperationDemandScope {
  HostSchoolOnly = 0,
  Alliance = 1,
  Public = 2,
}

export const cooperationDemandScopeOptions = mapEnumToOptions(CooperationDemandScope);
