import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { MasteryLevel } from '../enums/mastery-level.enum';
import type { StudentCourseStatus } from '../enums/student-course-status.enum';
import type { SelfAssessment } from '../enums/self-assessment.enum';

export interface ChapterProgressDto {
  chapterId?: string;
  chapterName?: string;
  totalExercises?: number;
  completedCount?: number;
  completionRate?: number;
  correctRate?: number;
}

export interface CourseLearningOverviewDto {
  courseId?: string;
  courseName?: string;
  totalStudents?: number;
  activeStudents?: number;
  totalExercises?: number;
  averageCompletionRate?: number;
  averageCorrectRate?: number;
  chapterProgress?: ChapterProgressDto[];
}

export interface GetCourseLearningOverviewInput {
  courseId?: string;
  tenantId?: string | null;
}

export interface GetLearningStatisticsInput extends PagedAndSortedResultRequestDto {
  courseId?: string;
  chapterId?: string | null;
  tenantId?: string | null;
  startTime?: string | null;
  endTime?: string | null;
}

export interface GetMyRecentRecordsInput extends PagedAndSortedResultRequestDto {
  courseId?: string | null;
  isCorrect?: number | null;
}

export interface GetStudentExerciseRecordsInput extends PagedAndSortedResultRequestDto {
  courseId?: string;
  chapterId?: string | null;
}

export interface KnowledgeDimensionDto {
  name?: string;
  maxValue?: number;
}

export interface KnowledgeMasteryDto extends EntityDto<string> {
  knowledgeResourceId?: string;
  knowledgeResourceName?: string;
  level?: MasteryLevel;
  practiceCount?: number;
  correctCount?: number;
  accuracy?: number;
  lastPracticeAt?: string;
}

export interface LearningDashboardDto {
  totalCourses?: number;
  completedCourses?: number;
  inProgressCourses?: number;
  notStartedCourses?: number;
  totalLearningTime?: number;
  averageProgress?: number;
  dailyTimeLabels?: string[];
  dailyTimeValues?: number[];
  knowledgeDimensions?: KnowledgeDimensionDto[];
  masteryValues?: number[];
  recentLearning?: RecentLearningDto[];
}

export interface LearningProgressDto extends EntityDto<string> {
  courseId?: string;
  chapterId?: string | null;
  resourceId?: string | null;
  progress?: number;
  lastPosition?: string | null;
  timeSpent?: string;
  lastAccessAt?: string;
}

export interface MarkAnswerViewedInput {
  exerciseId?: string;
  courseId?: string;
}

export interface RecentLearningDto {
  courseId?: string;
  courseName?: string;
  progress?: number;
  lastAccessAt?: string;
}

export interface RecordProgressInput {
  courseId?: string;
  chapterId?: string | null;
  resourceId?: string | null;
  progress?: number;
  lastPosition?: string | null;
  additionalMinutes?: number;
}

export interface SaveExerciseRecordInput {
  courseId?: string;
  chapterId?: string | null;
  exerciseId?: string;
  studentAnswer?: string | null;
  timeSpentTicks?: number;
}

export interface StudentCourseDto extends FullAuditedEntityDto<string> {
  courseId?: string;
  courseTitle?: string;
  courseCoverImageUrl?: string | null;
  major?: string | null;
  semester?: string | null;
  status?: StudentCourseStatus;
  enrolledAt?: string;
  startedAt?: string | null;
  completedAt?: string | null;
  progress?: number;
  credits?: number | null;
}

export interface StudentExerciseRecordDto extends FullAuditedEntityDto<string> {
  studentId?: string;
  studentName?: string | null;
  courseId?: string;
  courseName?: string | null;
  chapterId?: string | null;
  chapterName?: string | null;
  exerciseId?: string;
  exerciseTitle?: string | null;
  studentAnswer?: string | null;
  isCorrect?: boolean | null;
  hasViewedAnswer?: boolean;
  viewedAt?: string | null;
  selfAssessment?: SelfAssessment;
  timeSpent?: string;
  completedAt?: string | null;
}

export interface StudentLearningStatisticsDto {
  studentId?: string;
  studentName?: string;
  completedCount?: number;
  totalCount?: number;
  completionRate?: number;
  correctRate?: number;
  totalTimeSpent?: string;
  lastActiveTime?: string | null;
}

export interface SubmitSelfAssessmentInput {
  exerciseId?: string;
  courseId?: string;
  assessment?: SelfAssessment;
}
