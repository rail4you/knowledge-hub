import { Injectable, inject } from '@angular/core';
import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { cleanOptionContent } from '../../shared/exercise/option-content.util';

/** 与后端 KnowledgeHub.Exams.Enums.ExerciseType 对齐 */
export enum ExerciseType {
  SingleChoice = 0,
  MultiChoice = 1,
  TrueFalse = 2,
  FillBlank = 3,
  ShortAnswer = 4,
  Essay = 5,
  CaseAnalysis = 6,
  TermExplanation = 7,
  Calculation = 8,
  Programming = 9,
}

export enum ExamType {
  Quiz = 0,
  ChapterTest = 1,
  Midterm = 2,
  Final = 3,
}

export enum ExamStatus {
  NotStarted = 0,
  InProgress = 1,
  Submitted = 2,
  Graded = 3,
}

export interface ExamListItem {
  id: string;
  courseId: string;
  courseName?: string;
  chapterId?: string;
  title: string;
  description?: string;
  type: ExamType;
  durationMinutes: number;
  totalScore: number;
  passingScore: number;
  startTime?: string;
  endTime?: string;
  exerciseCount: number;
  submissionCount: number;
  pendingGradeCount: number;
  gradedCount: number;
  creationTime: string;
}

export interface ExamQuestion {
  exerciseId: string;
  sortOrder: number;
  score: number;
  title: string;
  questionContent: string;
  type: ExerciseType;
  options?: string;
  answer: string;
  questionAnalysis?: string;
  difficulty: number;
}

export interface ExamDetail extends ExamListItem {
  questions: ExamQuestion[];
  rules?: ExamTypeRule[];
}

export interface ExamTypeRule {
  type: ExerciseType;
  count: number;
  scorePerItem: number;
}

export interface GenerateExamInput {
  courseId: string;
  /** 主章节（兼容旧调用 / 试卷列表按章节筛选） */
  chapterId?: string | null;
  /** 限定章节（可多选）：为空 = 整门课程 */
  chapterIds?: string[] | null;
  title: string;
  description?: string | null;
  type: ExamType;
  durationMinutes: number;
  passingScore: number;
  startTime?: string | null;
  endTime?: string | null;
  rules: ExamTypeRule[];
  targetTotalScore?: number | null;
  shuffle: boolean;
}

export interface StudentExamListItem {
  examId: string;
  examTitle: string;
  description?: string;
  type: ExamType;
  durationMinutes: number;
  totalScore: number;
  passingScore: number;
  exerciseCount: number;
  startTime?: string;
  endTime?: string;
  studentExamId?: string;
  status: ExamStatus;
  myScore?: number;
  isPassed?: boolean;
  submittedAt?: string;
}

export interface ExamPaperQuestion {
  exerciseId: string;
  sortOrder: number;
  score: number;
  title: string;
  questionContent: string;
  type: ExerciseType;
  options?: string;
  difficulty: number;
  myAnswer?: string;
}

export interface StudentExamPaper {
  studentExamId: string;
  examId: string;
  examTitle: string;
  description?: string;
  durationMinutes: number;
  totalScore: number;
  passingScore: number;
  status: ExamStatus;
  startedAt?: string;
  submittedAt?: string;
  questions: ExamPaperQuestion[];
}

export interface StudentAnswerDetail {
  exerciseId: string;
  sortOrder: number;
  title: string;
  questionContent: string;
  type: ExerciseType;
  options?: string;
  maxScore: number;
  content?: string;
  standardAnswer?: string;
  questionAnalysis?: string;
  score?: number;
  feedback?: string;
  isCorrect?: boolean;
  isAiGraded: boolean;
}

export interface StudentExamResult {
  studentExamId: string;
  examId: string;
  examTitle: string;
  studentId: string;
  studentName?: string;
  status: ExamStatus;
  totalScore?: number;
  maxScore: number;
  isPassed?: boolean;
  startedAt?: string;
  submittedAt?: string;
  gradedAt?: string;
  answers: StudentAnswerDetail[];
}

export interface ExamSubmissionItem {
  studentExamId: string;
  examId: string;
  examTitle: string;
  studentId: string;
  studentName?: string;
  status: ExamStatus;
  totalScore?: number;
  maxScore: number;
  submittedAt?: string;
  gradedAt?: string;
  subjectiveCount: number;
}

export interface GradeSubmissionInput {
  answers: { exerciseId: string; score?: number | null; feedback?: string | null }[];
  comment?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ExamApiService {
  private readonly rest = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  private cfg(): Partial<Rest.Config> {
    return { apiName: this.apiName } as Partial<Rest.Config>;
  }

  // ── 教师端 ──
  getList = (input: {
    courseId?: string;
    chapterId?: string;
    type?: ExamType;
    filter?: string;
    skipCount?: number;
    maxResultCount?: number;
  }) =>
    this.rest.request<any, PagedResultDto<ExamListItem>>(
      {
        method: 'GET',
        url: '/api/app/exam',
        params: {
          courseId: input.courseId,
          chapterId: input.chapterId,
          type: input.type,
          filter: input.filter,
          skipCount: input.skipCount ?? 0,
          maxResultCount: input.maxResultCount ?? 10,
        },
      },
      this.cfg(),
    );

  get = (id: string) =>
    this.rest.request<any, ExamDetail>({ method: 'GET', url: `/api/app/exam/${id}` }, this.cfg());

  generate = (input: GenerateExamInput) =>
    this.rest.request<any, ExamDetail>(
      { method: 'POST', url: '/api/app/exam/generate', body: input },
      this.cfg(),
    );

  delete = (id: string) =>
    this.rest.request<any, void>({ method: 'DELETE', url: `/api/app/exam/${id}` }, this.cfg());

  getSubmissions = (input: {
    examId?: string;
    courseId?: string;
    status?: ExamStatus;
    filter?: string;
    skipCount?: number;
    maxResultCount?: number;
  }) =>
    this.rest.request<any, PagedResultDto<ExamSubmissionItem>>(
      {
        method: 'GET',
        url: '/api/app/exam/submissions',
        params: {
          examId: input.examId,
          courseId: input.courseId,
          status: input.status,
          filter: input.filter,
          skipCount: input.skipCount ?? 0,
          maxResultCount: input.maxResultCount ?? 10,
        },
      },
      this.cfg(),
    );

  getSubmission = (studentExamId: string) =>
    this.rest.request<any, StudentExamResult>(
      { method: 'GET', url: `/api/app/exam/submission/${studentExamId}` },
      this.cfg(),
    );

  grade = (studentExamId: string, input: GradeSubmissionInput) =>
    this.rest.request<any, StudentExamResult>(
      { method: 'POST', url: `/api/app/exam/grade/${studentExamId}`, body: input },
      this.cfg(),
    );

  autoGrade = (studentExamId: string) =>
    this.rest.request<any, StudentExamResult>(
      { method: 'POST', url: `/api/app/exam/auto-grade/${studentExamId}` },
      this.cfg(),
    );

  // ── 学生端 ──
  getMyExams = (courseId: string) =>
    this.rest.request<any, StudentExamListItem[]>(
      { method: 'GET', url: `/api/app/exam/my-exams/${courseId}` },
      this.cfg(),
    );

  start = (examId: string) =>
    this.rest.request<any, StudentExamPaper>(
      { method: 'POST', url: `/api/app/exam/start/${examId}` },
      this.cfg(),
    );

  submit = (studentExamId: string, answers: { exerciseId: string; content?: string | null }[]) =>
    this.rest.request<any, StudentExamResult>(
      {
        method: 'POST',
        url: `/api/app/exam/submit/${studentExamId}`,
        body: { answers },
      },
      this.cfg(),
    );

  getMyResult = (studentExamId: string) =>
    this.rest.request<any, StudentExamResult>(
      { method: 'GET', url: `/api/app/exam/my-result/${studentExamId}` },
      this.cfg(),
    );
}

export const EXERCISE_TYPE_LABELS: Record<number, string> = {
  [ExerciseType.SingleChoice]: '单选题',
  [ExerciseType.MultiChoice]: '多选题',
  [ExerciseType.TrueFalse]: '判断题',
  [ExerciseType.FillBlank]: '填空题',
  [ExerciseType.ShortAnswer]: '问答题',
  [ExerciseType.Essay]: '论述题',
  [ExerciseType.CaseAnalysis]: '案例分析',
  [ExerciseType.TermExplanation]: '名词解释',
  [ExerciseType.Calculation]: '计算题',
  [ExerciseType.Programming]: '编程题',
};

export const EXAM_TYPE_LABELS: Record<number, string> = {
  [ExamType.Quiz]: '随堂测验',
  [ExamType.ChapterTest]: '章节测验',
  [ExamType.Midterm]: '期中考试',
  [ExamType.Final]: '期末考试',
};

export const EXAM_STATUS_LABELS: Record<number, string> = {
  [ExamStatus.NotStarted]: '未开始',
  [ExamStatus.InProgress]: '作答中',
  [ExamStatus.Submitted]: '待评卷',
  [ExamStatus.Graded]: '已评卷',
};

/** 主观题（需要人工/AI 判卷） */
export function isSubjective(type: ExerciseType): boolean {
  return type >= ExerciseType.ShortAnswer;
}

/**
 * 解析习题选项。题库里存在两种历史格式：
 *  1) ["选项1","选项2", ...]（手动录入 / Excel 导入）
 *  2) [{key:"A", content:"选项"}, ...]（AI 生成 / 早期种子数据）
 * 统一成 { key, content } 供答题与判卷页面渲染。
 */
export function parseExerciseOptions(options?: string | null): { key: string; content: string }[] {
  if (!options) return [];
  try {
    const parsed = JSON.parse(options);
    if (!Array.isArray(parsed)) return [];
    return parsed
      .map((item: any, index: number) => {
        const fallbackKey = String.fromCharCode(65 + index);
        if (typeof item === 'string') {
          return { key: fallbackKey, content: cleanOptionContent(item) };
        }
        const key = (item?.key ?? item?.Key ?? fallbackKey) as string;
        const raw = item?.content ?? item?.Content ?? item?.value ?? item?.text ?? '';
        return { key: String(key), content: cleanOptionContent(String(raw)) };
      })
      .filter((x) => !!x.content);
  } catch {
    return [];
  }
}
