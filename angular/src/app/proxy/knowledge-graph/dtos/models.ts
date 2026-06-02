import type { EntityDto } from '@abp/ng.core';
import type { RelationType } from '../enums/relation-type.enum';

export interface KnowledgeGraphDto {
  courseId?: string;
  courseName?: string;
  nodes?: KnowledgeNodeDto[];
  relations?: KnowledgeRelationDto[];
}

export interface KnowledgeNodeDto extends EntityDto<string> {
  name?: string;
  description?: string | null;
  importanceLevel?: string;
  knowledgeResourceId?: string | null;
  masteryLevel?: number;
}

export interface KnowledgeRelationDto {
  id?: string;
  sourceNodeId?: string;
  targetNodeId?: string;
  type?: RelationType;
  weight?: number;
  description?: string | null;
}

export interface LearningPathDto {
  courseId?: string;
  nodes?: LearningPathNodeDto[];
  estimatedMinutes?: number;
  totalNodes?: number;
  completedNodes?: number;
}

export interface LearningPathNodeDto {
  nodeId?: string;
  nodeName?: string;
  sortOrder?: number;
  isCompleted?: boolean;
  prerequisiteNodeIds?: string[];
}
