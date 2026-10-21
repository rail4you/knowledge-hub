import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { ExerciseType } from '../../exams/enums/exercise-type.enum';

export interface AddKpExerciseLinkInput {
  knowledgePointId?: string;
  exerciseId?: string;
  weight?: number;
}

export interface AnalysisNotificationDto {
  id?: string;
  type?: string;
  title?: string;
  content?: string;
  source?: string;
  createdAt?: string;
  isRead?: boolean;
}

export interface AnalysisReportFileDto {
  id?: string;
  batchId?: string;
  type?: number;
  typeLabel?: string;
  fileName?: string;
  contentType?: string;
  sizeBytes?: number;
  storage?: string;
  downloadCount?: number;
  creationTime?: string;
  lastDownloadedAt?: string | null;
  metricsSnapshot?: string | null;
}

export interface ChapterStatDto {
  chapterId?: string | null;
  title?: string;
  rate?: number;
  stdDev?: number;
  topQuartile?: number;
  sampleSize?: number;
}

export interface ClassWeaknessDto {
  batchId?: string;
  batchTitle?: string;
  courseId?: string;
  courseName?: string;
  targetLine?: number;
  knowledgePoints?: KnowledgeMasteryItemDto[];
  focusStudents?: FocusStudentDto[];
  pushedCount?: number;
  totalStudents?: number;
}

export interface ConfirmPaperItemInput {
  id?: string;
  questionNo?: string | null;
  exerciseId?: string | null;
  score?: number;
  fullScore?: number;
}

export interface ConfirmPaperScriptInput {
  totalScore?: number;
  items?: ConfirmPaperItemInput[];
}

export interface CourseLinkConfigDto {
  courseId?: string;
  courseName?: string;
  path?: CoursePathNodeDto[];
  mappings?: PrerequisiteMappingDto[];
  links?: CourseLinkDto[];
}

export interface CourseLinkDto {
  id?: string;
  predecessorCourseId?: string;
  predecessorCourseTitle?: string;
  successorCourseId?: string;
  successorCourseTitle?: string;
}

export interface CoursePathNodeDto {
  courseId?: string;
  courseTitle?: string;
  state?: number;
  tag?: string;
  note?: string | null;
  prerequisiteNames?: string[];
}

export interface CourseStudentDto {
  studentId?: string;
  studentName?: string;
  studentNumber?: string;
  userName?: string;
  status?: number;
}

export interface CreatePaperBatchInput {
  courseId?: string;
  courseName?: string;
  title?: string;
  examDate?: string;
  fileNames?: string[];
}

export interface ExtractKnowledgePointsResultDto {
  courseId?: string;
  exerciseCount?: number;
  queued?: boolean;
  message?: string;
}

export interface FocusStudentDto {
  studentId?: string;
  studentName?: string;
  totalScore?: number;
  weakestNames?: string[];
  pushed?: boolean;
}

export interface GeneratePaperTemplateInput {
  courseId?: string;
  title?: string | null;
  studentId?: string | null;
  studentName?: string | null;
  studentNumber?: string | null;
  className?: string | null;
  examDate?: string | null;
  questionCount?: number | null;
  fullScorePerQuestion?: number | null;
  randomScore?: boolean | null;
  includeAnswer?: boolean | null;
  seed?: number | null;
}

export interface GetKnowledgePointListInput extends PagedAndSortedResultRequestDto {
  courseId?: string;
  filter?: string | null;
  status?: number | null;
  source?: string | null;
}

export interface KnowledgeMasteryItemDto {
  knowledgePointId?: string;
  name?: string;
  chapterTitle?: string | null;
  mastery?: number;
  evidence?: number;
  level?: number;
  isPredicted?: boolean;
  targetLine?: number;
  hasCustomThreshold?: boolean;
  insufficientData?: boolean;
}

export interface KnowledgePointDetailDto {
  id?: string;
  courseId?: string;
  chapterId?: string | null;
  chapterTitle?: string | null;
  name?: string;
  description?: string | null;
  syllabusOrder?: number;
  status?: number;
  source?: string;
  canonicalId?: string | null;
  canonicalName?: string | null;
  exerciseCount?: number;
  prerequisiteOutCount?: number;
  prerequisiteInCount?: number;
  hasEmbedding?: boolean;
  hasCustomThreshold?: boolean;
  targetLine?: number;
  weakLine?: number;
  creationTime?: string;
}

export interface KnowledgePointDto {
  id?: string;
  courseId?: string;
  chapterId?: string | null;
  chapterTitle?: string | null;
  name?: string;
  syllabusOrder?: number;
  status?: number;
  source?: string;
  canonicalId?: string | null;
}

export interface KnowledgePointGraphDto {
  courseId?: string;
  courseName?: string;
  nodes?: KnowledgePointGraphNodeDto[];
  edges?: KnowledgePointGraphEdgeDto[];
  externalNodeCount?: number;
  proposedEdgeCount?: number;
  mergedNodeCount?: number;
}

export interface KnowledgePointGraphEdgeDto {
  fromId?: string;
  toId?: string;
  type?: string;
  status?: number;
  strength?: number;
  note?: string | null;
}

export interface KnowledgePointGraphNodeDto {
  kp?: KnowledgePointDetailDto;
  isExternal?: boolean;
  courseTitle?: string | null;
  level?: number;
}

export interface KnowledgePointMergeResultDto {
  sourceId?: string;
  targetId?: string;
  movedExerciseLinks?: number;
  mergedExerciseLinks?: number;
  movedMasteries?: number;
  movedPrerequisites?: number;
  message?: string;
}

export interface KnowledgeVectorStatusDto {
  engine?: string;
  dimension?: number | null;
  available?: boolean;
  embeddedCount?: number;
  knowledgePointCount?: number;
  message?: string | null;
}

export interface KpExerciseLinkDto {
  id?: string;
  exerciseId?: string;
  exerciseTitle?: string;
  questionContent?: string | null;
  chapterId?: string | null;
  chapterTitle?: string | null;
  exerciseScore?: number;
  weight?: number;
  source?: string;
  knowledgePointId?: string;
  knowledgePointName?: string;
  knowledgePointStatus?: number;
}

export interface KpExerciseLinkItemInput {
  knowledgePointId?: string;
  weight?: number;
}

export interface KpExerciseMappingDto {
  exerciseId?: string;
  exerciseTitle?: string;
  questionContent?: string | null;
  chapterId?: string | null;
  chapterTitle?: string | null;
  exerciseScore?: number;
  links?: KpExerciseLinkDto[];
}

export interface KpGenerationResultDto {
  courseId?: string;
  queued?: boolean;
  chapterCount?: number;
  exerciseCount?: number;
  createdKnowledgePoints?: number;
  createdLinks?: number;
  rebuiltEmbeddings?: number;
  message?: string;
}

export interface KpMergeSuggestionDto {
  sourceId?: string;
  sourceName?: string;
  sourceStatus?: number;
  targetId?: string;
  targetName?: string;
  targetStatus?: number;
  similarity?: number;
  method?: string;
}

export interface LossAttributionDto {
  questionNo?: string;
  exerciseId?: string | null;
  exerciseTitle?: string;
  knowledgePointName?: string;
  mastery?: number;
  reason?: string;
  advice?: string;
}

export interface MasteryThresholdDto {
  id?: string | null;
  courseId?: string;
  knowledgePointId?: string | null;
  weakLine?: number;
  okLine?: number;
  isDefault?: boolean;
  isOverride?: boolean;
  courseWeakLine?: number | null;
  courseOkLine?: number | null;
}

export interface MergeKnowledgePointsInput {
  sourceId?: string;
  targetId?: string;
}

export interface MiningResultDto {
  proposedCount?: number;
  samplePairs?: number;
  message?: string;
}

export interface ModelTrainResultDto {
  trained?: boolean;
  sampleCount?: number;
  studentCount?: number;
  knowledgePointCount?: number;
  estimator?: string;
  message?: string;
}

export interface OcrCapabilitiesDto {
  image?: boolean;
  pdfDirect?: boolean;
  pdfModel?: string;
  note?: string | null;
}

export interface PaperAnalysisOverviewDto {
  batchCount?: number;
  scriptCount?: number;
  averageScore?: number;
  pendingConfirmCount?: number;
  pushedCount?: number;
  batches?: PaperBatchDto[];
}

export interface PaperBatchDto {
  id?: string;
  title?: string;
  courseId?: string;
  courseName?: string;
  examDate?: string;
  scriptCount?: number;
  needsConfirmCount?: number;
  pushedCount?: number;
  averageScore?: number;
  status?: number;
  uploadedCount?: number;
  processedCount?: number;
  failedCount?: number;
}

export interface PaperGeneratorConfigDto {
  defaultQuestionCount?: number;
  fullScorePerQuestion?: number;
  randomScore?: boolean;
  includeAnswer?: boolean;
  maxQuestionCount?: number;
  className?: string;
  titleTemplate?: string;
}

export interface PaperItemRowDto {
  id?: string;
  questionNo?: string;
  exerciseId?: string | null;
  exerciseTitle?: string;
  matchStatus?: string;
  score?: number;
  fullScore?: number;
  knowledgePointNames?: string[];
  ocrConfidence?: number;
}

export interface PaperScriptRowDto {
  id?: string;
  studentId?: string | null;
  studentName?: string;
  studentNumber?: string;
  totalScore?: number;
  ocrConfidence?: number;
  confirmed?: boolean;
  items?: PaperItemRowDto[];
}

export interface PaperTemplateDto {
  title?: string;
  courseId?: string;
  courseName?: string;
  studentName?: string;
  studentNumber?: string;
  className?: string;
  examDate?: string;
  questionCount?: number;
  totalFullScore?: number;
  totalScore?: number;
  questions?: PaperTemplateQuestionDto[];
  html?: string;
}

export interface PaperTemplateQuestionDto {
  no?: number;
  section?: string;
  typeName?: string;
  text?: string;
  options?: string[];
  fullScore?: number;
  score?: number;
  answer?: string;
}

export interface PaperUploadImageDto {
  id?: string;
  fileName?: string;
  sizeBytes?: number;
  pageIndex?: number;
  mergeGroup?: string | null;
  studentId?: string | null;
  studentName?: string;
  studentNumber?: string;
  status?: number;
  errorMessage?: string | null;
  scriptId?: string | null;
  isPdf?: boolean;
}

export interface PaperUploadItemDto {
  fileName?: string;
  contentType?: string;
  base64?: string;
  url?: string | null;
  pageIndex?: number;
  mergeGroup?: string | null;
}

export interface PracticeAnswerInput {
  exerciseId?: string;
  answer?: string | null;
  timeSpentTicks?: number;
}

export interface PracticeExerciseDto {
  id?: string;
  title?: string;
  questionContent?: string;
  type?: ExerciseType;
  options?: string | null;
  score?: number;
  difficulty?: number;
  chapterTitle?: string | null;
  knowledgePointNames?: string[];
}

export interface PracticeItemDto {
  id?: string;
  title?: string;
  courseId?: string;
  courseTitle?: string;
  exerciseCount?: number;
  knowledgePointId?: string | null;
  fromPrerequisite?: boolean;
  reason?: string | null;
  exerciseIds?: string[];
}

export interface PracticeItemResultDto {
  exerciseId?: string;
  title?: string;
  studentAnswer?: string | null;
  correctAnswer?: string | null;
  isCorrect?: boolean | null;
  analysis?: string | null;
}

export interface PracticeResultDto {
  totalCount?: number;
  correctCount?: number;
  wrongCount?: number;
  knowledgePoints?: KnowledgeMasteryItemDto[];
  items?: PracticeItemResultDto[];
}

export interface PracticeSetDto {
  courseId?: string;
  courseName?: string;
  knowledgePointId?: string | null;
  knowledgePointName?: string | null;
  reason?: string | null;
  items?: PracticeExerciseDto[];
  message?: string | null;
}

export interface PrerequisiteMappingDto {
  id?: string;
  successorChapterTitle?: string;
  dependencyNames?: string[];
  sourceCourseTitle?: string;
  condition?: string;
  status?: number;
  source?: string;
  toKpId?: string | null;
  fromKpIds?: string[];
}

export interface PushResultDto {
  pushedCount?: number;
}

export interface RebuildEmbeddingsResultDto {
  total?: number;
  updated?: number;
  provider?: string;
  engine?: string;
  dimension?: number | null;
  vectorStored?: number;
  message?: string;
}

export interface RecommendationEvaluationDto {
  courseId?: string | null;
  studentCount?: number;
  evaluatedStudentCount?: number;
  answerCount?: number;
  topK?: number;
  results?: RecommendationWeightSetDto[];
  best?: RecommendationWeightSetDto | null;
  current?: RecommendationWeightSetDto | null;
  message?: string;
}

export interface RecommendationWeightSetDto {
  name?: string;
  content?: number;
  collab?: number;
  fit?: number;
  label?: string;
  weakHitRate?: number;
  precision?: number;
  hits?: number;
  weakTotal?: number;
}

export interface RecommendationWeightsDto {
  content?: number;
  collab?: number;
  fit?: number;
  label?: string;
  source?: string;
}

export interface ReviewPackageItemDto {
  knowledgePointId?: string;
  name?: string;
  courseId?: string;
  courseTitle?: string;
  mastery?: number;
  level?: number;
  isCompleted?: boolean;
}

export interface ReviewPackageProgressDto {
  reviewPackageId?: string;
  upcomingCourseId?: string;
  upcomingCourseTitle?: string;
  completed?: number;
  total?: number;
  items?: ReviewPackageItemDto[];
}

export interface SaveCourseLinkInput {
  predecessorCourseId?: string;
  successorCourseId?: string;
}

export interface SaveKpExerciseLinksInput {
  exerciseId?: string;
  links?: KpExerciseLinkItemInput[];
}

export interface SavePrerequisiteInput {
  id?: string | null;
  courseId?: string;
  toKpId?: string | null;
  successorChapterTitle?: string | null;
  fromKpIds?: string[];
  dependencyNames?: string[];
  sourceCourseTitle?: string | null;
  condition?: string | null;
  source?: string;
  status?: number;
}

export interface SetThresholdInput {
  courseId?: string;
  knowledgePointId?: string | null;
  weakLine?: number;
  okLine?: number;
}

export interface SplitKnowledgePointInput {
  knowledgePointId?: string;
  newName?: string;
  chapterId?: string | null;
  exerciseIds?: string[];
}

export interface StudentAnalysisDashboardDto {
  overview?: StudentWeaknessOverviewDto;
  path?: CoursePathNodeDto[];
  reviewProgress?: ReviewPackageProgressDto | null;
  notifications?: AnalysisNotificationDto[];
  unreadCount?: number;
  practices?: PracticeItemDto[];
}

export interface StudentReportPreviewDto {
  studentId?: string | null;
  studentName?: string;
  studentNumber?: string;
  title?: string;
  content?: string;
  items?: LossAttributionDto[];
  pushed?: boolean;
  pushedAt?: string | null;
}

export interface StudentWeaknessOverviewDto {
  studentName?: string;
  courseId?: string;
  courseName?: string;
  paperBatchId?: string | null;
  paperTitle?: string;
  score?: number | null;
  classAverage?: number | null;
  classRank?: number | null;
  classSize?: number | null;
  confirmed?: boolean;
  sourceLabel?: string;
  weakCount?: number;
  knowledgePoints?: KnowledgeMasteryItemDto[];
  losses?: LossAttributionDto[];
}

export interface SubmitPracticeInput {
  courseId?: string;
  knowledgePointId?: string | null;
  answers?: PracticeAnswerInput[];
}

export interface TeachingDiagnosisDto {
  level?: string;
  chapterTitle?: string;
  rate?: number;
  gap?: number;
  advice?: string;
}

export interface TeachingInsightDto {
  batchId?: string;
  courseId?: string;
  courseName?: string;
  averageRate?: number;
  deltaVsPrevious?: number;
  belowTargetCount?: number;
  targetLine?: number;
  chapters?: ChapterStatDto[];
  diagnoses?: TeachingDiagnosisDto[];
  generatedAt?: string;
}

export interface UpdateKnowledgePointInput {
  name?: string;
  chapterId?: string | null;
  description?: string | null;
  syllabusOrder?: number;
}

export interface UpdateKpExerciseLinkInput {
  linkId?: string;
  weight?: number;
}

export interface UploadPaperScriptInput {
  courseId?: string;
  courseName?: string;
  title?: string;
  examDate?: string;
  studentId?: string;
  studentName?: string;
  studentNumber?: string;
  files?: PaperUploadItemDto[];
}

export interface UploadPaperScriptResultDto {
  batchId?: string;
  batchTitle?: string;
  imageId?: string;
  studentId?: string;
  studentName?: string;
}
