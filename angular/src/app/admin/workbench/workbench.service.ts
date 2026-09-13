import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RestService } from '@abp/ng.core';

export interface WorkbenchResourceStats {
  total: number;
  draft: number;
  pendingReview: number;
  approved: number;
  rejected: number;
  categoryCount: number;
  totalDownloads: number;
  totalViews: number;
  totalCollections: number;
}

export interface WorkbenchCourseStats {
  total: number;
  published: number;
  pendingReview: number;
  draft: number;
  chapterCount: number;
  exerciseCount: number;
  majorCount: number;
  microMajorCount: number;
  microMajorPublished: number;
  enrollmentCount: number;
  microMajorEnrollmentCount: number;
}

export interface WorkbenchAiStats {
  totalCalls: number;
  successCalls: number;
  failedCalls: number;
  runningCalls: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  estimatedCost: number;
  taskCount: number;
}

export interface WorkbenchEmploymentStats {
  jobTotal: number;
  jobPublished: number;
  applicationCount: number;
  interviewCount: number;
  outcomeCount: number;
  signedCount: number;
  employedCount: number;
}

export interface WorkbenchPracticumStats {
  projectTotal: number;
  projectPublished: number;
  taskCount: number;
  enrollmentCount: number;
  submissionCount: number;
}

export interface WorkbenchNewsStats {
  total: number;
  published: number;
  pendingReview: number;
}

export interface WorkbenchSearchStats {
  totalSearches: number;
  todaySearches: number;
  activeUsers: number;
}

export interface WorkbenchUserStats {
  studentCount: number;
  teacherCount: number;
}

export interface WorkbenchUsage {
  searches: number;
  resourceUploads: number;
  resourceViews: number;
  aiCalls: number;
  enrollments: number;
  jobApplications: number;
  practicumSubmissions: number;
}

export interface WorkbenchDailyUsage extends WorkbenchUsage {
  date: string;
  label: string;
}

export interface WorkbenchTrends {
  daily: WorkbenchDailyUsage[];
  today: WorkbenchUsage;
  yesterday: WorkbenchUsage;
  lastDays: WorkbenchUsage;
}

export interface WorkbenchStats {
  tenantId?: string | null;
  tenantName?: string | null;
  generatedAt: string;
  resources: WorkbenchResourceStats;
  courses: WorkbenchCourseStats;
  ai: WorkbenchAiStats;
  employment: WorkbenchEmploymentStats;
  practicum: WorkbenchPracticumStats;
  news: WorkbenchNewsStats;
  search: WorkbenchSearchStats;
  users: WorkbenchUserStats;
  trends: WorkbenchTrends;
}

@Injectable({
  providedIn: 'root',
})
export class WorkbenchService {
  private readonly restService = inject(RestService);
  private readonly apiUrl = '/api/app/workbench';

  /**
   * 获取工作台统计。
   * @param tenantId 仅 host 生效：传租户 Id 查看该租户，传空汇总全部租户。
   */
  getStats(tenantId?: string | null): Observable<WorkbenchStats> {
    const params: Record<string, any> = {};
    if (tenantId) {
      params.tenantId = tenantId;
    }

    return this.restService.request(
      {
        method: 'GET',
        url: `${this.apiUrl}/stats`,
        params,
      },
      { apiName: 'Default' },
    );
  }
}
