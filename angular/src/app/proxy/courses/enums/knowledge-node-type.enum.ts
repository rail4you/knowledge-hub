import { mapEnumToOptions } from '@abp/ng.core';

export enum KnowledgeNodeType {
  Unit = 0,
  Item = 1,
  Element = 2,
}

export const knowledgeNodeTypeOptions = mapEnumToOptions(KnowledgeNodeType);
