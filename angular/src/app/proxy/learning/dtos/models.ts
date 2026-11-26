import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { KnowledgeNodeType } from '../../courses/enums/knowledge-node-type.enum';
import type { MasteryLevel } from '../enums/mastery-level.enum';
import type { StudentActivityAction } from '../enums/student-activity-action.enum';
import type { StudentRecommendationType } from '../enums/student-recommendation-type.enum';
import type { StudentCourseStatus } from '../enums/student-course-status.enum';
import type { ExerciseType } from '../../exams/enums/exercise-type.enum';
import type { SelfAssessment } from '../enums/self-assessment.enum';
import type { StudentRecommendationStatus } from '../enums/student-recommendation-status.enum';

export interface ChapterProgressDto {
  chapterId?: string;
  chapterName?: string;
  totalExercises?: number;
  completedCount?: number;
  completionRate?: number;
  correctRate?: number;
  participantCount?: number;
}

export interface CourseExerciseProgressDto {
  courseId?: string;
  completedCount?: number;
  totalCount?: number;
}

export interface CourseInsightDto {
  courseId?: string;
  courseTitle?: string | null;
  days?: number;
  metrics?: InsightMetricDto[];
  findings?: InsightFindingDto[];
}

export interface CourseLearningOverviewDto {
  courseId?: string;
  courseName?: string;
  totalStudents?: number;
  activeStudents?: number;
  totalExercises?: number;
  averageCompletionRate?: number;
  averageCorrectRate?: number;
  totalLearningMinutes?: number;
  chapterProgress?: ChapterProgressDto[];
}

export interface CourseLearningProfileDto {
  courseId?: string;
  courseTitle?: string | null;
  days?: number;
  studentCount?: number;
  activeStudentCount?: number;
  activityCount?: number;
  activityPerStudent?: number;
  averageConsistency?: number;
  recentActivityLevel?: number;
  timeline?: ProfileTrendPointDto[];
  actionDistribution?: ProfileActionCountDto[];
  topActiveStudents?: StudentActivitySummaryDto[];
  inactiveStudents?: StudentActivitySummaryDto[];
}

export interface CourseRecommendationOverviewDto {
  courseId?: string;
  courseTitle?: string | null;
  activeCount?: number;
  completedCount?: number;
  dismissedCount?: number;
  adoptRate?: number;
  studentCount?: number;
  typeDistribution?: RecommendationTypeCountDto[];
  students?: StudentRecommendationSummaryDto[];
}

export interface CourseStatisticsItemDto {
  courseId?: string;
  courseName?: string;
  totalStudents?: number;
  activeStudents?: number;
  totalExercises?: number;
  chapterCount?: number;
  averageCompletionRate?: number;
  averageCorrectRate?: number;
  lastActiveTime?: string | null;
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

export interface GetStudentLearningDetailInput {
  courseId?: string;
  studentId?: string;
}

export interface GetTenantCourseStatisticsInput {
  tenantId?: string | null;
}

export interface InsightFindingDto {
  level?: string;
  title?: string;
  detail?: string;
}

export interface InsightMetricDto {
  key?: string;
  label?: string;
  value?: string;
  hint?: string | null;
}

export interface KnowledgeDimensionDto {
  name?: string;
  maxValue?: number;
}

export interface KnowledgeMasteryDto extends EntityDto<string> {
  knowledgeResourceId?: string;
  knowledgeResourceName?: string;
  parentId?: string | null;
  nodeType?: KnowledgeNodeType;
  sortPath?: string | null;
  level?: MasteryLevel;
  mastery?: number;
  evidence?: number;
  hasEvidence?: boolean;
  grade?: number;
  weakLine?: number;
  okLine?: number;
  practiceCount?: number;
  correctCount?: number;
  accuracy?: number;
  lastPracticeAt?: string;
  updatedAt?: string;
}

export interface LearningDashboardDto {
  totalCourses?: number;
  completedCourses?: number;
  inProgressCourses?: number;
  notStartedCourses?: number;
  totalLearningTime?: number;
  totalExerciseRecords?: number;
  totalResourceActivities?: number;
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

export interface ProfileActionCountDto {
  action?: StudentActivityAction;
  label?: string;
  count?: number;
}

export interface ProfileReportDto {
  courseId?: string;
  courseTitle?: string | null;
  template?: string;
  templateLabel?: string;
  days?: number;
  generatedAt?: string;
  sections?: ProfileReportSectionDto[];
}

export interface ProfileReportSectionDto {
  title?: string;
  paragraphs?: string[];
  table?: ProfileReportTableDto | null;
}

export interface ProfileReportTableDto {
  headers?: string[];
  rows?: [string][];
}

export interface ProfileTrendPointDto {
  date?: string;
  count?: number;
}

export interface ProfileWeakPointDto {
  knowledgeResourceId?: string;
  name?: string;
  mastery?: number;
  evidence?: number;
}

export interface RecentLearningDto {
  courseId?: string;
  courseName?: string;
  progress?: number;
  lastAccessAt?: string;
}

export interface RecommendationRefreshResultDto {
  courseId?: string;
  studentCount?: number;
  createdCount?: number;
  updatedCount?: number;
  activeCount?: number;
  message?: string;
}

export interface RecommendationTypeCountDto {
  type?: StudentRecommendationType;
  label?: string;
  count?: number;
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

export interface StudentActivitySummaryDto {
  studentId?: string;
  studentName?: string | null;
  activityCount?: number;
  lastActiveAt?: string | null;
}

export interface StudentChapterLearningDetailDto {
  chapterId?: string;
  chapterName?: string;
  totalExercises?: number;
  completedCount?: number;
  correctCount?: number;
  completionRate?: number;
  correctRate?: number;
  timeSpent?: string;
  records?: StudentExerciseRecordDto[];
}

export interface StudentCourseListItemDto extends EntityDto<string> {
  courseId?: string;
  courseTitle?: string;
  courseCoverImageUrl?: string | null;
  majorId?: string | null;
  majorName?: string | null;
  semester?: string | null;
  status?: StudentCourseStatus;
  enrolledAt?: string;
  startedAt?: string | null;
  completedAt?: string | null;
  progress?: number;
  credits?: number | null;
  chapterCount?: number;
  semesterHours?: number | null;
  completedExerciseCount?: number;
  totalExerciseCount?: number;
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
  exerciseType?: ExerciseType;
  studentAnswer?: string | null;
  isCorrect?: boolean | null;
  hasViewedAnswer?: boolean;
  viewedAt?: string | null;
  selfAssessment?: SelfAssessment;
  timeSpent?: string;
  completedAt?: string | null;
}

export interface StudentLearningDetailDto {
  studentId?: string;
  studentName?: string;
  loginAccount?: string;
  completedCount?: number;
  totalCount?: number;
  completionRate?: number;
  correctRate?: number;
  totalTimeSpent?: string;
  lastActiveTime?: string | null;
  chapters?: StudentChapterLearningDetailDto[];
}

export interface StudentLearningProfileDto {
  studentId?: string;
  studentName?: string | null;
  courseId?: string | null;
  activityCount?: number;
  lastActiveAt?: string | null;
  preferredActionLabel?: string | null;
  preferredAction?: StudentActivityAction | null;
  activePeriod?: string | null;
  consistencyScore?: number;
  recentActivityLevel?: number;
  insights?: string[];
  actionDistribution?: ProfileActionCountDto[];
  weakPoints?: ProfileWeakPointDto[];
  timeline?: ProfileTrendPointDto[];
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

export interface StudentRecommendationDto {
  id?: string;
  studentId?: string;
  studentName?: string | null;
  courseId?: string;
  type?: StudentRecommendationType;
  typeLabel?: string;
  status?: StudentRecommendationStatus;
  statusLabel?: string;
  priority?: number;
  reason?: string;
  knowledgeResourceId?: string | null;
  knowledgeResourceName?: string | null;
  chapterId?: string | null;
  chapterTitle?: string | null;
  metricValue?: number | null;
  handledAt?: string | null;
  creationTime?: string;
  actionKind?: string;
}

export interface StudentRecommendationSummaryDto {
  studentId?: string;
  studentName?: string | null;
  activeCount?: number;
  completedCount?: number;
  dismissedCount?: number;
  lastCreatedAt?: string | null;
  items?: StudentRecommendationDto[];
}

export interface SubmitSelfAssessmentInput {
  exerciseId?: string;
  courseId?: string;
  assessment?: SelfAssessment;
}

export interface TenantCourseStatisticsDto {
  totalCourses?: number;
  totalStudents?: number;
  activeStudents?: number;
  totalExercises?: number;
  averageCompletionRate?: number;
  averageCorrectRate?: number;
  courses?: CourseStatisticsItemDto[];
}
