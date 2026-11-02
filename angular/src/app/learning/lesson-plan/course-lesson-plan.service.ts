import { Injectable, inject } from '@angular/core';
import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';

/** 与后端 KnowledgeHub.Courses.CourseLessonPlanSource 对齐 */
export enum CourseLessonPlanSource {
  Upload = 0,
  Ai = 1,
}

export interface CourseLessonPlanDto {
  id: string;
  courseId: string;
  courseName?: string;
  chapterId?: string;
  chapterTitle?: string;
  title: string;
  description?: string;
  source: CourseLessonPlanSource;
  fileUrl?: string;
  fileName?: string;
  fileExtension?: string;
  fileSize?: number;
  contentJson?: string;
  aiTaskId?: string;
  creatorName?: string;
  creationTime: string;
}

export interface CourseLessonPlanQuery {
  courseId?: string;
  chapterId?: string;
  source?: CourseLessonPlanSource;
  filter?: string;
  skipCount?: number;
  maxResultCount?: number;
}

export interface CreateCourseLessonPlanInput {
  courseId: string;
  chapterId?: string;
  chapterTitle?: string;
  title: string;
  description?: string;
  fileUrl: string;
  fileName?: string;
  fileExtension?: string;
  fileSize?: number;
}

@Injectable({ providedIn: 'root' })
export class CourseLessonPlanService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getList = (input: CourseLessonPlanQuery) =>
    this.restService.request<any, PagedResultDto<CourseLessonPlanDto>>(
      {
        method: 'GET',
        url: '/api/app/course-lesson-plan',
        params: {
          courseId: input.courseId,
          chapterId: input.chapterId,
          source: input.source,
          filter: input.filter,
          skipCount: input.skipCount ?? 0,
          maxResultCount: input.maxResultCount ?? 10,
        },
      },
      { apiName: this.apiName } as Partial<Rest.Config>,
    );

  get = (id: string) =>
    this.restService.request<any, CourseLessonPlanDto>(
      { method: 'GET', url: `/api/app/course-lesson-plan/${id}` },
      { apiName: this.apiName } as Partial<Rest.Config>,
    );

  create = (input: CreateCourseLessonPlanInput) =>
    this.restService.request<any, CourseLessonPlanDto>(
      { method: 'POST', url: '/api/app/course-lesson-plan', body: input },
      { apiName: this.apiName } as Partial<Rest.Config>,
    );

  delete = (id: string) =>
    this.restService.request<any, void>(
      { method: 'DELETE', url: `/api/app/course-lesson-plan/${id}` },
      { apiName: this.apiName } as Partial<Rest.Config>,
    );
}
