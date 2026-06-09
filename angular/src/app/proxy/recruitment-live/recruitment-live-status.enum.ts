import { mapEnumToOptions } from '@abp/ng.core';

export enum RecruitmentLiveStatus {
  Waiting = 0,
  Active = 1,
  Ended = 2,
  Cancelled = 3,
}

export const recruitmentLiveStatusOptions = mapEnumToOptions(RecruitmentLiveStatus);
