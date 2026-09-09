import { mapEnumToOptions } from '@abp/ng.core';

export enum PracticumChatMessageType {
  Text = 0,
  File = 1,
  Image = 2,
}

export const practicumChatMessageTypeOptions = mapEnumToOptions(PracticumChatMessageType);
