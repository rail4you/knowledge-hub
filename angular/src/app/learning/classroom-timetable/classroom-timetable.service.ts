import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { Observable } from 'rxjs';

export interface TimetableItem {
  taskId: string;
  title: string;
  description?: string | null;
  courseName?: string | null;
  courseId?: string | null;
  agentName?: string | null;
  targetType: number;
  kind: 'assign' | 'due' | string;
  at: string;
  endAt?: string | null;
  schoolName?: string | null;
  publishStatus: number;
  assignmentCount: number;
  completedCount: number;
  needsHelpCount: number;
  isOverdue: boolean;
}

export interface TimetableCourse {
  id: string;
  name: string;
  taskCount: number;
}

export interface Timetable {
  from: string;
  to: string;
  totalCount: number;
  taskCount: number;
  courseCount: number;
  overdueCount: number;
  items: TimetableItem[];
  courses: TimetableCourse[];
}

const BASE = '/api/app/classroom-agent-task';

/**
 * 互动课堂课表：数据源是「课堂智能体任务」。
 *
 * 老师给智能体分配任务时设截止时间，课表把任务排到日/周/月视图，
 * hover 显示任务名称 / 日期 / 时间 / 学校。
 */
@Injectable({ providedIn: 'root' })
export class ClassroomTimetableService {
  private readonly rest = inject(RestService);

  get = (from: string, to: string, courseId?: string | null): Observable<Timetable> =>
    this.rest.request<any, Timetable>(
      {
        method: 'GET',
        url: `${BASE}/timetable`,
        params: { from, to, courseId: courseId || undefined, publishedOnly: true },
      },
      { apiName: 'KnowledgeHub' },
    );
}
