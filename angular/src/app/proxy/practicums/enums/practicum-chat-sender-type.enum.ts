import { mapEnumToOptions } from '@abp/ng.core';

export enum PracticumChatSenderType {
  Student = 0,
  Teacher = 1,
  AIAgent = 2,
}

export const practicumChatSenderTypeOptions = mapEnumToOptions(PracticumChatSenderType);
