import { Injectable, inject } from '@angular/core';
import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';

/** 与后端 KnowledgeHub.Courses.CourseCaseSource 对齐 */
export enum CourseCaseSource {
  Manual = 0,
  Import = 1,
  Ai = 2,
}

export interface CourseCaseBackground {
  industry: string;
  timeframe: string;
  context: string;
  stakeholders: string[];
}

export interface CourseCaseKeyIssue {
  id: string;
  title: string;
  description: string;
  impact: string;
  /** 高 / 中 / 低 */
  severity: string;
}

export interface CourseCaseSolution {
  id: string;
  title: string;
  description: string;
  steps: string[];
  expectedOutcome: string;
}

export interface CourseCaseContent {
  title: string;
  summary: string;
  background: CourseCaseBackground;
  keyIssues: CourseCaseKeyIssue[];
  solutions: CourseCaseSolution[];
  keyInsights: string[];
  recommendations: string[];
}

export interface CourseCaseDto {
  id: string;
  courseId: string;
  courseName?: string;
  chapterId?: string;
  chapterTitle?: string;
  title: string;
  summary?: string;
  industry?: string;
  timeframe?: string;
  contentJson?: string;
  content: CourseCaseContent;
  source: CourseCaseSource;
  aiTaskId?: string;
  creatorName?: string;
  creationTime: string;
}

export interface CourseCaseQuery {
  courseId?: string;
  chapterId?: string;
  source?: CourseCaseSource;
  filter?: string;
  skipCount?: number;
  maxResultCount?: number;
}

export interface CreateCourseCaseInput {
  courseId: string;
  chapterId?: string | null;
  chapterTitle?: string | null;
  title: string;
  summary?: string | null;
  content?: CourseCaseContent | null;
}

export interface UpdateCourseCaseInput {
  title: string;
  summary?: string | null;
  chapterId?: string | null;
  chapterTitle?: string | null;
  content?: CourseCaseContent | null;
}

export interface CourseCaseImportResult {
  totalRows: number;
  successCount: number;
  failCount: number;
  errors: string[];
}

/** 案例内容空结构（新建表单初值） */
export function emptyCourseCaseContent(title = ''): CourseCaseContent {
  return {
    title,
    summary: '',
    background: { industry: '', timeframe: '', context: '', stakeholders: [] },
    keyIssues: [],
    solutions: [],
    keyInsights: [],
    recommendations: [],
  };
}

@Injectable({ providedIn: 'root' })
export class CourseCaseService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getList = (input: CourseCaseQuery) =>
    this.restService.request<any, PagedResultDto<CourseCaseDto>>(
      {
        method: 'GET',
        url: '/api/app/course-case',
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
    this.restService.request<any, CourseCaseDto>(
      { method: 'GET', url: `/api/app/course-case/${id}` },
      { apiName: this.apiName } as Partial<Rest.Config>,
    );

  create = (input: CreateCourseCaseInput) =>
    this.restService.request<any, CourseCaseDto>(
      { method: 'POST', url: '/api/app/course-case', body: input },
      { apiName: this.apiName } as Partial<Rest.Config>,
    );

  update = (id: string, input: UpdateCourseCaseInput) =>
    this.restService.request<any, CourseCaseDto>(
      { method: 'PUT', url: `/api/app/course-case/${id}`, body: input },
      { apiName: this.apiName } as Partial<Rest.Config>,
    );

  delete = (id: string) =>
    this.restService.request<any, void>(
      { method: 'DELETE', url: `/api/app/course-case/${id}` },
      { apiName: this.apiName } as Partial<Rest.Config>,
    );

  importFromExcel = (courseId: string, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return this.restService.request<any, CourseCaseImportResult>(
      { method: 'POST', url: `/api/app/course-case/import-from-excel/${courseId}`, body: formData },
      { apiName: this.apiName } as Partial<Rest.Config>,
    );
  };
}
