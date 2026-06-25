import type { ExerciseType } from '../enums/exercise-type.enum';
import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateUpdateExerciseDto {
  courseId?: string;
  chapterId?: string | null;
  chapterIds?: string[];
  knowledgeResourceId?: string | null;
  title?: string;
  questionContent?: string;
  type?: ExerciseType;
  options?: string | null;
  answer?: string;
  answerExplanation?: string | null;
  difficulty?: number;
  score?: number;
}

export interface ExerciseDto extends FullAuditedEntityDto<string> {
  courseId?: string;
  chapterId?: string | null;
  chapterIds?: string[];
  knowledgeResourceId?: string | null;
  title?: string;
  questionContent?: string;
  type?: ExerciseType;
  options?: string | null;
  answer?: string;
  answerExplanation?: string | null;
  difficulty?: number;
  score?: number;
  isAiGenerated?: boolean;
}

export interface ExerciseImportResultDto {
  totalRows?: number;
  successCount?: number;
  failCount?: number;
  errors?: string[];
}

export interface GenerateExerciseInput {
  courseId?: string;
  knowledgeResourceId?: string | null;
  type?: ExerciseType;
  count?: number;
  difficulty?: number;
  topicHint?: string | null;
}

export interface GradeEssayInput {
  studentExamId?: string;
  exerciseId?: string;
  standardAnswer?: string;
  maxScore?: number;
}

export interface GradingResultDto {
  score?: number;
  feedback?: string | null;
  isCorrect?: boolean;
}
