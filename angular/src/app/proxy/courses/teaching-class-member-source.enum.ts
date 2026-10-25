import { mapEnumToOptions } from '@abp/ng.core';

export enum TeachingClassMemberSource {
  Manual = 0,
  Import = 1,
  SelfJoin = 2,
}

export const teachingClassMemberSourceOptions = mapEnumToOptions(TeachingClassMemberSource);
