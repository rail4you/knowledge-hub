import type { ExamType } from '../enums/exam-type.enum';
import type { ExerciseType } from '../enums/exercise-type.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { ExamStatus } from '../enums/exam-status.enum';

export interface AiAnalyzeExerciseInput {
  exerciseIds?: string[];
}

export interface AiAnalyzeExerciseResultDto {
  updatedCount?: number;
  totalCount?: number;
  errors?: string[];
}

export interface CreateExamDto {
  courseId?: string;
  chapterId?: string | null;
  title?: string;
  description?: string | null;
  type?: ExamType;
  durationMinutes?: number;
  passingScore?: number;
  startTime?: string | null;
  endTime?: string | null;
  exercises?: ExamExerciseItemDto[];
}

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
  questionAnalysis?: string | null;
  difficulty?: number;
  score?: number;
}

export interface ExamDetailDto extends ExamDto {
  courseName?: string | null;
  questions?: ExamQuestionDto[];
  rules?: ExamTypeRuleDto[] | null;
}

export interface ExamDto extends FullAuditedEntityDto<string> {
  courseId?: string;
  chapterId?: string | null;
  title?: string;
  description?: string | null;
  type?: ExamType;
  durationMinutes?: number;
  totalScore?: number;
  passingScore?: number;
  startTime?: string | null;
  endTime?: string | null;
  exerciseCount?: number;
}

export interface ExamExerciseItemDto {
  exerciseId?: string;
  score?: number;
  sortOrder?: number;
}

export interface ExamListItemDto extends ExamDto {
  courseName?: string | null;
  submissionCount?: number;
  pendingGradeCount?: number;
  gradedCount?: number;
}

export interface ExamListQueryDto extends PagedAndSortedResultRequestDto {
  courseId?: string | null;
  chapterId?: string | null;
  type?: ExamType | null;
  filter?: string | null;
}

export interface ExamPaperQuestionDto {
  exerciseId?: string;
  sortOrder?: number;
  score?: number;
  title?: string;
  questionContent?: string;
  type?: ExerciseType;
  options?: string | null;
  difficulty?: number;
  myAnswer?: string | null;
}

export interface ExamQuestionDto {
  exerciseId?: string;
  sortOrder?: number;
  score?: number;
  title?: string;
  questionContent?: string;
  type?: ExerciseType;
  options?: string | null;
  answer?: string;
  questionAnalysis?: string | null;
  difficulty?: number;
}

export interface ExamSubmissionItemDto {
  studentExamId?: string;
  examId?: string;
  examTitle?: string;
  studentId?: string;
  studentName?: string | null;
  status?: ExamStatus;
  totalScore?: number | null;
  maxScore?: number;
  submittedAt?: string | null;
  gradedAt?: string | null;
  subjectiveCount?: number;
}

export interface ExamSubmissionQueryDto extends PagedAndSortedResultRequestDto {
  examId?: string | null;
  courseId?: string | null;
  status?: ExamStatus | null;
  filter?: string | null;
}

export interface ExamTypeRuleDto {
  type?: ExerciseType;
  count?: number;
  scorePerItem?: number;
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
  questionAnalysis?: string | null;
  difficulty?: number;
  score?: number;
  isAiGenerated?: boolean;
  creatorName?: string | null;
  ownerTenantName?: string | null;
  canEdit?: boolean;
}

export interface ExerciseImportResultDto {
  totalRows?: number;
  successCount?: number;
  failCount?: number;
  errors?: string[];
}

export interface GenerateExamInput {
  courseId?: string;
  chapterId?: string | null;
  chapterIds?: string[] | null;
  title?: string;
  description?: string | null;
  type?: ExamType;
  durationMinutes?: number;
  passingScore?: number;
  startTime?: string | null;
  endTime?: string | null;
  rules?: ExamTypeRuleDto[];
  targetTotalScore?: number | null;
  shuffle?: boolean;
}

export interface GenerateExerciseInput {
  courseId?: string;
  knowledgeResourceId?: string | null;
  chapterId?: string | null;
  chapterIds?: string[];
  type?: ExerciseType;
  count?: number;
  difficulty?: number;
  topicHint?: string | null;
  customPrompt?: string | null;
}

export interface GradeAnswerItemDto {
  exerciseId?: string;
  score?: number | null;
  feedback?: string | null;
}

export interface GradeEssayInput {
  studentExamId?: string;
  exerciseId?: string;
  standardAnswer?: string;
  maxScore?: number;
}

export interface GradeSubmissionInput {
  answers?: GradeAnswerItemDto[];
  comment?: string | null;
}

export interface GradingResultDto {
  score?: number;
  feedback?: string | null;
  isCorrect?: boolean;
}

export interface StudentAnswerDetailDto {
  exerciseId?: string;
  sortOrder?: number;
  title?: string;
  questionContent?: string;
  type?: ExerciseType;
  options?: string | null;
  maxScore?: number;
  content?: string | null;
  standardAnswer?: string | null;
  questionAnalysis?: string | null;
  score?: number | null;
  feedback?: string | null;
  isCorrect?: boolean | null;
  isAiGraded?: boolean;
}

export interface StudentExamListItemDto {
  examId?: string;
  examTitle?: string;
  description?: string | null;
  type?: ExamType;
  durationMinutes?: number;
  totalScore?: number;
  passingScore?: number;
  exerciseCount?: number;
  startTime?: string | null;
  endTime?: string | null;
  studentExamId?: string | null;
  status?: ExamStatus;
  myScore?: number | null;
  isPassed?: boolean | null;
  submittedAt?: string | null;
}

export interface StudentExamPaperDto {
  studentExamId?: string;
  examId?: string;
  examTitle?: string;
  description?: string | null;
  durationMinutes?: number;
  totalScore?: number;
  passingScore?: number;
  status?: ExamStatus;
  startedAt?: string | null;
  submittedAt?: string | null;
  questions?: ExamPaperQuestionDto[];
}

export interface StudentExamResultDto {
  studentExamId?: string;
  examId?: string;
  examTitle?: string;
  studentId?: string;
  studentName?: string | null;
  status?: ExamStatus;
  totalScore?: number | null;
  maxScore?: number;
  isPassed?: boolean | null;
  startedAt?: string | null;
  submittedAt?: string | null;
  gradedAt?: string | null;
  answers?: StudentAnswerDetailDto[];
}

export interface SubmitAnswerItem {
  exerciseId?: string;
  content?: string | null;
}

export interface SubmitExamInput {
  answers?: SubmitAnswerItem[];
}
