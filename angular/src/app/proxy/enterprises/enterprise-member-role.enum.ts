import { mapEnumToOptions } from '@abp/ng.core';

export enum EnterpriseMemberRole {
  Admin = 0,
  Member = 1,
}

export const enterpriseMemberRoleOptions = mapEnumToOptions(EnterpriseMemberRole);
