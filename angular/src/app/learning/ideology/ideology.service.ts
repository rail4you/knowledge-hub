import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Observable } from 'rxjs';

/** 与后端 KnowledgeHub.CourseIdeology.Enums 对齐 */
export enum IdeologyCaseStatus {
  Draft = 0,
  Published = 10,
  Archived = 20,
}

export enum IdeologySourceType {
  Textbook = 0,
  AuthorityMedia = 1,
  Law = 2,
  Enterprise = 3,
  AiGenerated = 4,
  Other = 5,
}

export enum IdeologyUsageMethod {
  Import = 0,
  CaseAnalysis = 1,
  Discussion = 2,
  PracticeComment = 3,
  ExtendedReading = 4,
}

export enum IdeologyInsetPosition {
  Import = 0,
  Lecture = 1,
  Practice = 2,
  Summary = 3,
  Homework = 4,
}

export const IDEOLOGY_STATUS_LABEL: Record<number, string> = {
  [IdeologyCaseStatus.Draft]: '草稿',
  [IdeologyCaseStatus.Published]: '已发布',
  [IdeologyCaseStatus.Archived]: '已下架',
};

export const IDEOLOGY_SOURCE_LABEL: Record<number, string> = {
  [IdeologySourceType.Textbook]: '教材',
  [IdeologySourceType.AuthorityMedia]: '权威媒体',
  [IdeologySourceType.Law]: '法律法规',
  [IdeologySourceType.Enterprise]: '校企合作',
  [IdeologySourceType.AiGenerated]: 'AI 生成',
  [IdeologySourceType.Other]: '其它',
};

export const IDEOLOGY_USAGE_LABEL: Record<number, string> = {
  [IdeologyUsageMethod.Import]: '导入·讨论',
  [IdeologyUsageMethod.CaseAnalysis]: '案例分析',
  [IdeologyUsageMethod.Discussion]: '课堂讨论',
  [IdeologyUsageMethod.PracticeComment]: '实训点评',
  [IdeologyUsageMethod.ExtendedReading]: '拓展阅读',
};

export const IDEOLOGY_POSITION_LABEL: Record<number, string> = {
  [IdeologyInsetPosition.Import]: '导入',
  [IdeologyInsetPosition.Lecture]: '讲授',
  [IdeologyInsetPosition.Practice]: '实训/练习',
  [IdeologyInsetPosition.Summary]: '小结',
  [IdeologyInsetPosition.Homework]: '课后作业',
};

export interface IdeologyElementDto {
  id: string;
  name: string;
  category?: string | null;
  description?: string | null;
  sortOrder: number;
  isEnabled: boolean;
  caseCount?: number;
}

export interface IdeologyCaseContent {
  entryPoint?: string;
  brief?: string;
  discussionQuestions?: string[];
  integrationSuggestion?: string;
}

export interface IdeologyCaseDto {
  id: string;
  courseId: string;
  chapterId?: string | null;
  chapterTitle?: string | null;
  title: string;
  summary?: string | null;
  contentJson?: string | null;
  sourceType: IdeologySourceType;
  sourceText: string;
  sourceUrl?: string | null;
  sourceDate?: string | null;
  usageMethod: IdeologyUsageMethod;
  status: IdeologyCaseStatus;
  aiGenerated: boolean;
  aiMaterial?: string | null;
  creatorName?: string | null;
  publishedAt?: string | null;
  creationTime: string;
  elementIds: string[];
  elementNames: string[];
}

export interface CreateUpdateIdeologyCase {
  courseId: string;
  chapterId?: string | null;
  chapterTitle?: string | null;
  title: string;
  summary?: string | null;
  contentJson?: string | null;
  sourceType: IdeologySourceType;
  sourceText: string;
  sourceUrl?: string | null;
  sourceDate?: string | null;
  usageMethod: IdeologyUsageMethod;
  elementIds: string[];
  aiGenerated?: boolean;
  aiMaterial?: string | null;
  publish?: boolean;
}

export interface IdeologyGraphNode {
  id: string;
  title: string;
  weight: number;
  status?: IdeologyCaseStatus | null;
}

export interface IdeologyGraphEdge {
  kind: string;
  from: string;
  to: string;
}

export interface IdeologyGraphDto {
  courseId: string;
  courseTitle: string;
  chapters: IdeologyGraphNode[];
  cases: IdeologyGraphNode[];
  elements: IdeologyGraphNode[];
  edges: IdeologyGraphEdge[];
  gaps: IdeologyGraphNode[];
}

export interface IdeologyCoverageItem {
  id: string;
  name: string;
  caseCount: number;
  percent: number;
}

export interface IdeologyCoverageDto {
  courseId: string;
  courseTitle: string;
  totalCases: number;
  publishedCases: number;
  draftCases: number;
  archivedCases: number;
  aiGeneratedCases: number;
  ideologyGoal?: string | null;
  chapters: IdeologyCoverageItem[];
  elements: IdeologyCoverageItem[];
  gaps: string[];
  generatedAt: string;
}

export interface CourseIdeologySettingDto {
  courseId: string;
  courseTitle?: string | null;
  isEnabled: boolean;
  ideologyGoal?: string | null;
  note?: string | null;
  caseCount: number;
}

export interface IdeologyInsetDto {
  id: string;
  lessonPlanId: string;
  courseId: string;
  caseId?: string | null;
  elementId?: string | null;
  position: IdeologyInsetPosition;
  title?: string | null;
  content?: string | null;
  sortOrder: number;
  sourceText?: string | null;
}

export interface IdeologyRecommendItem {
  caseId: string;
  title: string;
  summary?: string | null;
  usageMethod: IdeologyUsageMethod;
  sourceText: string;
  elementNames: string[];
  reason: string;
}

export interface IdeologyCaseDraft {
  title: string;
  summary?: string | null;
  contentJson?: string | null;
  sourceText: string;
  warnings: string[];
}

/**
 * 课程思政模块 API（手写 service，不走 proxy）。
 *
 * <p>原因：本模块是**插件式**的 —— 后端按 Feature 开关启停，
 * 若把接口生成进 `proxy/**`，模块下线后 proxy 里会留下一堆指向不存在接口的方法；
 * 手写 service 让"删模块 = 删这一个文件 + 删页面目录"。</p>
 */
@Injectable({ providedIn: 'root' })
export class IdeologyService {
  private readonly rest = inject(RestService);

  // ── 思政元素 ────────────────────────────────────────────
  getElements = (onlyEnabled = false): Observable<IdeologyElementDto[]> =>
    this.rest.request<any, IdeologyElementDto[]>({
      method: 'GET',
      url: '/api/app/ideology-element',
      params: { onlyEnabled },
    });

  createElement = (body: Partial<IdeologyElementDto>): Observable<IdeologyElementDto> =>
    this.rest.request<any, IdeologyElementDto>({
      method: 'POST',
      url: '/api/app/ideology-element',
      body,
    });

  updateElement = (id: string, body: Partial<IdeologyElementDto>): Observable<IdeologyElementDto> =>
    this.rest.request<any, IdeologyElementDto>({
      method: 'PUT',
      url: `/api/app/ideology-element/${id}`,
      body,
    });

  deleteElement = (id: string): Observable<void> =>
    this.rest.request<any, void>({ method: 'DELETE', url: `/api/app/ideology-element/${id}` });

  // ── 案例 ────────────────────────────────────────────────
  getCases = (params: {
    courseId: string;
    chapterId?: string | null;
    elementId?: string | null;
    status?: IdeologyCaseStatus | null;
    filter?: string;
    skipCount?: number;
    maxResultCount?: number;
  }): Observable<PagedResultDto<IdeologyCaseDto>> =>
    this.rest.request<any, PagedResultDto<IdeologyCaseDto>>({
      method: 'GET',
      url: '/api/app/ideology-case',
      params: {
        courseId: params.courseId,
        chapterId: params.chapterId || undefined,
        elementId: params.elementId || undefined,
        status: params.status ?? undefined,
        filter: params.filter || undefined,
        skipCount: params.skipCount ?? 0,
        maxResultCount: params.maxResultCount ?? 10,
      },
    });

  getCase = (id: string): Observable<IdeologyCaseDto> =>
    this.rest.request<any, IdeologyCaseDto>({ method: 'GET', url: `/api/app/ideology-case/${id}` });

  createCase = (body: CreateUpdateIdeologyCase): Observable<IdeologyCaseDto> =>
    this.rest.request<any, IdeologyCaseDto>({
      method: 'POST',
      url: '/api/app/ideology-case',
      body,
    });

  updateCase = (id: string, body: CreateUpdateIdeologyCase): Observable<IdeologyCaseDto> =>
    this.rest.request<any, IdeologyCaseDto>({
      method: 'PUT',
      url: `/api/app/ideology-case/${id}`,
      body,
    });

  deleteCase = (id: string): Observable<void> =>
    this.rest.request<any, void>({ method: 'DELETE', url: `/api/app/ideology-case/${id}` });

  publishCase = (id: string): Observable<IdeologyCaseDto> =>
    this.rest.request<any, IdeologyCaseDto>({
      method: 'POST',
      url: `/api/app/ideology-case/${id}/publish`,
    });

  archiveCase = (id: string): Observable<IdeologyCaseDto> =>
    this.rest.request<any, IdeologyCaseDto>({
      method: 'POST',
      url: `/api/app/ideology-case/${id}/archive`,
    });

  // ── 图谱 / 覆盖分析 ──────────────────────────────────────
  getGraph = (courseId: string): Observable<IdeologyGraphDto> =>
    this.rest.request<any, IdeologyGraphDto>({
      method: 'GET',
      url: '/api/app/ideology-graph',
      params: { courseId },
    });

  getCoverage = (courseId: string): Observable<IdeologyCoverageDto> =>
    this.rest.request<any, IdeologyCoverageDto>({
      method: 'GET',
      url: '/api/app/ideology-coverage',
      params: { courseId },
    });

  /** 导出是 POST（后端 ExportAsync(Guid courseId) → POST /export/{courseId}）。 */
  getCoverageExportUrl = (courseId: string): string => `/api/app/ideology-coverage/export/${courseId}`;

  // ── 课程设置 ────────────────────────────────────────────
  getSetting = (courseId: string): Observable<CourseIdeologySettingDto> =>
    this.rest.request<any, CourseIdeologySettingDto>({
      method: 'GET',
      url: '/api/app/course-ideology-setting',
      params: { courseId },
    });

  saveSetting = (body: {
    courseId: string;
    isEnabled: boolean;
    ideologyGoal?: string | null;
    note?: string | null;
  }): Observable<CourseIdeologySettingDto> =>
    this.rest.request<any, CourseIdeologySettingDto>({
      method: 'POST',
      url: '/api/app/course-ideology-setting/save',
      body,
    });

  // ── 教案融入 ────────────────────────────────────────────
  getInsets = (lessonPlanId: string): Observable<IdeologyInsetDto[]> =>
    this.rest.request<any, IdeologyInsetDto[]>({
      method: 'GET',
      url: `/api/app/ideology-inset/by-lesson-plan/${lessonPlanId}`,
    });

  createInset = (body: {
    lessonPlanId: string;
    caseId?: string | null;
    elementId?: string | null;
    position: IdeologyInsetPosition;
    title?: string | null;
    content?: string | null;
    sortOrder?: number;
  }): Observable<IdeologyInsetDto> =>
    this.rest.request<any, IdeologyInsetDto>({
      method: 'POST',
      url: '/api/app/ideology-inset',
      body: { sortOrder: 0, ...body },
    });

  deleteInset = (id: string): Observable<void> =>
    this.rest.request<any, void>({ method: 'DELETE', url: `/api/app/ideology-inset/${id}` });

  getRecommend = (lessonPlanId: string): Observable<IdeologyRecommendItem[]> =>
    this.rest.request<any, IdeologyRecommendItem[]>({
      method: 'POST',
      url: `/api/app/ideology-inset/recommend/${lessonPlanId}`,
    });

  // ── AI 生成 ─────────────────────────────────────────────
  generateDraft = (body: {
    courseId: string;
    chapterId?: string | null;
    elementIds: string[];
    material: string;
    usageMethod: IdeologyUsageMethod;
    extraPrompt?: string | null;
  }): Observable<IdeologyCaseDraft> =>
    this.rest.request<any, IdeologyCaseDraft>({
      method: 'POST',
      url: '/api/app/ideology-ai/generate-draft',
      body,
    });

  // ── 工具 ────────────────────────────────────────────────
  /** contentJson → 结构化对象（解析失败降级为空对象，不阻塞编辑）。 */
  static parseContent(json?: string | null): IdeologyCaseContent {
    if (!json) return {};
    try {
      return JSON.parse(json) as IdeologyCaseContent;
    } catch {
      return {};
    }
  }

  /**
   * 结构化内容 → contentJson。
   *
   * <p>只保留 <c>brief</c>（案例文本）：改版后不再有「切入点 / 讨论问题 / 融入建议」，
   * 旧数据里这些字段仍能读出来（`parseContent` 兼容），但保存时会被收敛掉。</p>
   */
  static stringifyContent(content: IdeologyCaseContent): string {
    return JSON.stringify({ brief: content.brief ?? '' });
  }

  /**
   * 取出案例正文（兼容历史数据）：
   * 新数据在 `brief`；老数据可能是 `entryPoint + brief + discussionQuestions` 拼起来的。
   */
  static caseText(json: string | null, maxChars = 4000): string {
    const content = IdeologyService.parseContent(json);
    const parts: string[] = [];
    if (content.brief?.trim()) parts.push(content.brief.trim());
    if (content.entryPoint?.trim()) parts.push(`切入点：${content.entryPoint.trim()}`);
    const questions = (content.discussionQuestions ?? []).filter(x => !!x?.trim());
    if (questions.length) {
      parts.push('讨论问题：' + questions.map((q, i) => `${i + 1}. ${q}`).join(' '));
    }
    if (content.integrationSuggestion?.trim()) {
      parts.push(`融入建议：${content.integrationSuggestion.trim()}`);
    }

    const text = parts.join('\n\n');
    return text.length <= maxChars ? text : text.slice(0, maxChars) + '…';
  }
}
