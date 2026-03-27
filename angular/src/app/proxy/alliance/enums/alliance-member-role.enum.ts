import { mapEnumToOptions } from '@abp/ng.core';

export enum AllianceMemberRole {
  Member = 0,
  Admin = 1,
}

export const allianceMemberRoleOptions = mapEnumToOptions(AllianceMemberRole);
