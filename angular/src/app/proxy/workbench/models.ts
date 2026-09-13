
export interface WorkbenchAiStatsDto {
  totalCalls?: number;
  successCalls?: number;
  failedCalls?: number;
  runningCalls?: number;
  totalInputTokens?: number;
  totalOutputTokens?: number;
  estimatedCost?: number;
  taskCount?: number;
}

export interface WorkbenchCourseStatsDto {
  total?: number;
  published?: number;
  pendingReview?: number;
  draft?: number;
  chapterCount?: number;
  exerciseCount?: number;
  majorCount?: number;
  microMajorCount?: number;
  microMajorPublished?: number;
  enrollmentCount?: number;
  microMajorEnrollmentCount?: number;
}

export interface WorkbenchDailyUsageDto extends WorkbenchUsageDto {
  date?: string;
  label?: string;
}

export interface WorkbenchEmploymentStatsDto {
  jobTotal?: number;
  jobPublished?: number;
  applicationCount?: number;
  interviewCount?: number;
  outcomeCount?: number;
  signedCount?: number;
  employedCount?: number;
}

export interface WorkbenchNewsStatsDto {
  total?: number;
  published?: number;
  pendingReview?: number;
}

export interface WorkbenchPracticumStatsDto {
  projectTotal?: number;
  projectPublished?: number;
  taskCount?: number;
  enrollmentCount?: number;
  submissionCount?: number;
}

export interface WorkbenchQueryDto {
  tenantId?: string | null;
}

export interface WorkbenchResourceStatsDto {
  total?: number;
  draft?: number;
  pendingReview?: number;
  approved?: number;
  rejected?: number;
  categoryCount?: number;
  totalDownloads?: number;
  totalViews?: number;
  totalCollections?: number;
}

export interface WorkbenchSearchStatsDto {
  totalSearches?: number;
  todaySearches?: number;
  activeUsers?: number;
}

export interface WorkbenchStatsDto {
  tenantId?: string | null;
  tenantName?: string | null;
  generatedAt?: string;
  resources?: WorkbenchResourceStatsDto;
  courses?: WorkbenchCourseStatsDto;
  ai?: WorkbenchAiStatsDto;
  employment?: WorkbenchEmploymentStatsDto;
  practicum?: WorkbenchPracticumStatsDto;
  news?: WorkbenchNewsStatsDto;
  search?: WorkbenchSearchStatsDto;
  users?: WorkbenchUserStatsDto;
  trends?: WorkbenchTrendsDto;
}

export interface WorkbenchTrendsDto {
  daily?: WorkbenchDailyUsageDto[];
  today?: WorkbenchUsageDto;
  yesterday?: WorkbenchUsageDto;
  lastDays?: WorkbenchUsageDto;
}

export interface WorkbenchUsageDto {
  searches?: number;
  resourceUploads?: number;
  resourceViews?: number;
  aiCalls?: number;
  enrollments?: number;
  jobApplications?: number;
  practicumSubmissions?: number;
}

export interface WorkbenchUserStatsDto {
  studentCount?: number;
  teacherCount?: number;
}
