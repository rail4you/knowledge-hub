import type { IdeologyInsetPosition } from '../enums/ideology-inset-position.enum';
import type { IdeologySourceType } from '../enums/ideology-source-type.enum';
import type { IdeologyUsageMethod } from '../enums/ideology-usage-method.enum';
import type { IdeologyCaseStatus } from '../enums/ideology-case-status.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CourseIdeologySettingDto {
  courseId?: string;
  courseTitle?: string | null;
  isEnabled?: boolean;
  ideologyGoal?: string | null;
  note?: string | null;
  caseCount?: number;
}

export interface CreateIdeologyInsetDto {
  lessonPlanId?: string;
  caseId?: string | null;
  elementId?: string | null;
  position?: IdeologyInsetPosition;
  title?: string | null;
  content?: string | null;
  sortOrder?: number;
}

export interface CreateUpdateIdeologyCaseDto {
  courseId?: string;
  chapterId?: string | null;
  chapterTitle?: string | null;
  title?: string;
  summary?: string | null;
  contentJson?: string | null;
  sourceType?: IdeologySourceType;
  sourceText?: string;
  sourceUrl?: string | null;
  sourceDate?: string | null;
  usageMethod?: IdeologyUsageMethod;
  elementIds?: string[];
  aiGenerated?: boolean;
  aiMaterial?: string | null;
  publish?: boolean;
}

export interface CreateUpdateIdeologyElementDto {
  name?: string;
  category?: string | null;
  description?: string | null;
  sortOrder?: number;
  isEnabled?: boolean;
}

export interface GenerateIdeologyCaseInputDto {
  courseId?: string;
  chapterId?: string | null;
  elementIds?: string[];
  material?: string;
  usageMethod?: IdeologyUsageMethod;
  extraPrompt?: string | null;
}

export interface IdeologyCaseDraftDto {
  title?: string;
  summary?: string | null;
  contentJson?: string | null;
  sourceText?: string;
  warnings?: string[];
}

export interface IdeologyCaseDto {
  id?: string;
  courseId?: string;
  chapterId?: string | null;
  chapterTitle?: string | null;
  title?: string;
  summary?: string | null;
  contentJson?: string | null;
  sourceType?: IdeologySourceType;
  sourceText?: string;
  sourceUrl?: string | null;
  sourceDate?: string | null;
  usageMethod?: IdeologyUsageMethod;
  status?: IdeologyCaseStatus;
  aiGenerated?: boolean;
  aiMaterial?: string | null;
  creatorName?: string | null;
  publishedAt?: string | null;
  creationTime?: string;
  elementIds?: string[];
  elementNames?: string[];
}

export interface IdeologyCaseQueryDto extends PagedAndSortedResultRequestDto {
  courseId?: string;
  chapterId?: string | null;
  elementId?: string | null;
  status?: IdeologyCaseStatus | null;
  filter?: string | null;
}

export interface IdeologyCoverageDto {
  courseId?: string;
  courseTitle?: string;
  totalCases?: number;
  publishedCases?: number;
  draftCases?: number;
  archivedCases?: number;
  aiGeneratedCases?: number;
  ideologyGoal?: string | null;
  chapters?: IdeologyCoverageItemDto[];
  elements?: IdeologyCoverageItemDto[];
  gaps?: string[];
  generatedAt?: string;
}

export interface IdeologyCoverageItemDto {
  id?: string;
  name?: string;
  caseCount?: number;
  percent?: number;
}

export interface IdeologyElementDto {
  id?: string;
  name?: string;
  category?: string | null;
  description?: string | null;
  sortOrder?: number;
  isEnabled?: boolean;
  caseCount?: number;
}

export interface IdeologyGraphDto {
  courseId?: string;
  courseTitle?: string;
  chapters?: IdeologyGraphNodeDto[];
  cases?: IdeologyGraphNodeDto[];
  elements?: IdeologyGraphNodeDto[];
  edges?: IdeologyGraphEdgeDto[];
  gaps?: IdeologyGraphNodeDto[];
}

export interface IdeologyGraphEdgeDto {
  kind?: string;
  from?: string;
  to?: string;
}

export interface IdeologyGraphNodeDto {
  id?: string;
  title?: string;
  weight?: number;
  status?: IdeologyCaseStatus | null;
}

export interface IdeologyInsetDto {
  id?: string;
  lessonPlanId?: string;
  courseId?: string;
  caseId?: string | null;
  elementId?: string | null;
  position?: IdeologyInsetPosition;
  title?: string | null;
  content?: string | null;
  sortOrder?: number;
  sourceText?: string | null;
}

export interface IdeologyRecommendItemDto {
  caseId?: string;
  title?: string;
  summary?: string | null;
  usageMethod?: IdeologyUsageMethod;
  sourceText?: string;
  elementNames?: string[];
  reason?: string;
}

export interface SaveCourseIdeologySettingDto {
  courseId?: string;
  isEnabled?: boolean;
  ideologyGoal?: string | null;
  note?: string | null;
}
