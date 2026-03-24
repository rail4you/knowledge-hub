import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';
import type { MasteryLevel } from '../enums/mastery-level.enum';
import type { StudentCourseStatus } from '../enums/student-course-status.enum';

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
