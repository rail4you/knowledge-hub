import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import { ExerciseType } from '../enums/exercise-type.enum';

export interface ExerciseDto extends EntityDto<string> {
  courseId?: string;
  chapterId?: string;
  knowledgeResourceId?: string;
  title?: string;
  questionContent?: string;
  type?: ExerciseType;
  options?: string;
  answer?: string;
  answerExplanation?: string;
  difficulty?: number;
  score?: number;
  isAiGenerated?: boolean;
}

export interface CreateUpdateExerciseDto {
  courseId?: string;
  chapterId?: string;
  knowledgeResourceId?: string;
  title?: string;
  questionContent?: string;
  type?: ExerciseType;
  options?: string;
  answer?: string;
  answerExplanation?: string;
  difficulty?: number;
  score?: number;
}

export interface PagedExerciseRequestDto extends PagedAndSortedResultRequestDto {
  courseId?: string;
  chapterId?: string;
  type?: ExerciseType;
}
