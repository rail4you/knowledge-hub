import { Injectable, inject, signal } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable, of, delay, concat, map, catchError, throwError } from 'rxjs';

import {
  AnalysisReportFile,
  ClassWeaknessDto,
  CourseLinkConfig,
  CourseLinkItem,
  CreatePaperBatchInput,
  ExtractKnowledgePointsResult,
  MasteryThreshold,
  MiningResult,
  ModelTrainResult,
  OcrPipelineStep,
  PaperAnalysisOverview,
  PaperBatchItem,
  PaperBatchStatus,
  PaperScriptRow,
  PaperUploadImage,
  PaperUploadPayload,
  PrerequisiteMapping,
  RecommendationEvaluation,
  RecommendationWeights,
  SaveCourseLinkInput,
  StudentReportPreview,
  StudentAnalysisDashboard,
  TeachingInsightDto,
} from '../models/learning-analytics.models';
import {
  MOCK_CLASS_WEAKNESS,
  MOCK_COURSE_LINK,
  MOCK_PAPER_OVERVIEW,
  MOCK_SCRIPT_ROWS,
  MOCK_STUDENT_DASHBOARD,
  MOCK_TEACHING_INSIGHT,
} from './learning-analytics-mock';

/** OCR 流水线步骤定义（与教师端原型 steps 对齐） */
const OCR_STEPS: OcrPipelineStep[] = [
  { key: 'upload', label: '① 上传试卷', status: 'pending' },
  { key: 'ocr', label: '② OCR识别学号与得分', status: 'pending' },
  { key: 'match', label: '③ 题目匹配习题库知识点', status: 'pending' },
  { key: 'report', label: '④ 生成报告并推送', status: 'pending' },
];

/** 后端约定：`LearningAnalyticsAppService` → `/api/app/learning-analytics` */
const BASE = '/api/app/learning-analytics';

/**
 * 智学分析服务（学生端 + 教师端共用）。
 *
 * - 优先调用真实后端接口（`KnowledgeHub.LearningAnalytics` 模块）。
 * - 后端不可用 / 未部署时自动回退到 Mock 数据，保证 UI 原型可预览
 *   （回退时控制台输出 `[智学分析] 使用示例数据`，页面会展示「示例数据」标记）。
 */
@Injectable({ providedIn: 'root' })
export class LearningAnalyticsService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  /** 是否允许在后端不可用时回退到示例数据（原型预览用） */
  allowMockFallback = true;

  /** 当前页面是否正在展示示例数据（接口不可用时置 true，UI 据此显示标记） */
  readonly usingSampleData = signal(false);

  /**
   * 是否使用真实后端接口。
   * 当接口返回 401/403/404 或网络错误时，按 `allowMockFallback` 决定是否回退示例数据。
   */
  private api<T>(request: any): Observable<T> {
    return this.restService.request(request, { apiName: this.apiName }) as Observable<T>;
  }

  /** API 优先，失败回退 Mock（并提示） */
  private apiOrMock<T>(request: any, mock: T, label: string): Observable<T> {
    return this.api<T>(request).pipe(
      catchError(err => {
        if (!this.allowMockFallback) return throwError(() => err);
        // eslint-disable-next-line no-console
        console.warn(`[智学分析] 接口不可用，展示示例数据：${label}`, err?.status ?? err);
        this.usingSampleData.set(true);
        return of(mock).pipe(delay(240));
      })
    );
  }

  // ════════════════════════════════════════════════════════
  //  学生端
  // ════════════════════════════════════════════════════════

  /** 学生端整页聚合：薄弱点 + 学习路径 + 消息 + 专项练习 */
  getStudentDashboard(): Observable<StudentAnalysisDashboard> {
    return this.apiOrMock<StudentAnalysisDashboard>(
      { method: 'GET', url: `${BASE}/student-dashboard` },
      MOCK_STUDENT_DASHBOARD,
      '学生端学情总览'
    );
  }

  /** 标记消息已读 */
  markNotificationRead(id: string): Observable<void> {
    return this.api<void>({ method: 'POST', url: `${BASE}/notification/${id}/read` }).pipe(
      catchError(() => of(void 0))
    );
  }

  // ════════════════════════════════════════════════════════
  //  教师端 · 纸质试卷
  // ════════════════════════════════════════════════════════

  getPaperOverview(): Observable<PaperAnalysisOverview> {
    return this.apiOrMock<PaperAnalysisOverview>(
      { method: 'GET', url: `${BASE}/paper-overview` },
      MOCK_PAPER_OVERVIEW,
      '试卷批次概览'
    );
  }

  /** 创建批次（上传扫描件/照片） */
  createPaperBatch(input: CreatePaperBatchInput): Observable<PaperBatchItem> {
    return this.api<PaperBatchItem>({ method: 'POST', url: `${BASE}/paper-batch`, body: input }).pipe(
      catchError(() => {
        if (!this.allowMockFallback) return throwError(() => new Error('创建批次失败'));
        return of({
          id: `local-${Date.now()}`,
          title: input.title,
          courseId: input.courseId,
          courseName: input.courseName,
          examDate: input.examDate,
          scriptCount: 42,
          needsConfirmCount: 0,
          pushedCount: 0,
          averageScore: 0,
          status: PaperBatchStatus.Draft,
        } as PaperBatchItem).pipe(delay(400));
      })
    );
  }

  /**
   * 触发 OCR（后端入队执行，前端需要轮询批次状态）。
   * 返回步骤状态流仅用于「未接后端时的示例动画」；真实模式下请配合 `getPaperBatch` 轮询。
   */
  runPaperOcr(batchId: string): Observable<OcrPipelineStep[]> {
    const allDone = () => OCR_STEPS.map(s => ({ ...s, status: 'done' as const }));

    const mockStream = () => {
      let acc = OCR_STEPS.map(s => ({ ...s }));
      acc = acc.map((s, i) => (i === 0 ? { ...s, status: 'done' as const } : s));
      const emissions: Observable<OcrPipelineStep[]>[] = [of(acc.map(s => ({ ...s }))).pipe(delay(120))];
      for (let step = 1; step < OCR_STEPS.length; step++) {
        emissions.push(
          of(acc.map((s, i) => (i === step ? { ...s, status: 'running' as const } : { ...s }))).pipe(
            delay(step === 1 ? 460 : 620)
          )
        );
        acc = acc.map((s, i) => (i === step ? { ...s, status: 'done' as const } : s));
        emissions.push(of(acc.map(s => ({ ...s }))).pipe(delay(220)));
      }
      return concat(...emissions);
    };

    return this.api<void>({ method: 'POST', url: `${BASE}/paper-batch/${batchId}/run-ocr` }).pipe(
      map(() => allDone()),
      catchError(() => (this.allowMockFallback ? mockStream() : throwError(() => new Error('OCR 执行失败'))))
    );
  }

  getPaperScripts(batchId: string): Observable<PaperScriptRow[]> {
    return this.apiOrMock<PaperScriptRow[]>(
      { method: 'GET', url: `${BASE}/paper-batch/${batchId}/scripts` },
      MOCK_SCRIPT_ROWS,
      '试卷识别结果'
    );
  }

  /** 校对并确认单份试卷（确认后写入真实作答记录） */
  confirmPaperScript(scriptId: string, rows: PaperScriptRow[]): Observable<void> {
    const script = rows.find(r => r.id === scriptId);
    return this.api<void>({
      method: 'POST',
      url: `${BASE}/paper-script/${scriptId}/confirm`,
      body: { items: script?.items ?? [], totalScore: script?.totalScore ?? 0 },
    }).pipe(catchError(() => of(void 0).pipe(delay(200))));
  }

  /** 一键推送学生个人薄弱点报告 */
  pushPaperReports(batchId: string): Observable<{ pushedCount: number }> {
    return this.api<{ pushedCount: number }>({ method: 'POST', url: `${BASE}/paper-batch/${batchId}/push` }).pipe(
      catchError(() => of({ pushedCount: 42 }).pipe(delay(500)))
    );
  }

  // ════════════════════════════════════════════════════════
  //  报告导出 / 推送预览
  // ════════════════════════════════════════════════════════

  /** 导出 Word 文字报告：type=class（班级学情+教学诊断）| students（学生薄弱点汇总） */
  exportDocxReport(batchId: string, type: 'class' | 'students' = 'class'): Observable<Blob> {
    return this.api<Blob>({
      method: 'POST',
      url: `${BASE}/export-docx-report/${batchId}`,
      params: { type },
      responseType: 'blob',
    });
  }

  /** 导出学生薄弱点明细（Excel，供二次分析） */
  exportStudentReports(batchId: string): Observable<Blob> {
    return this.api<Blob>({ method: 'POST', url: `${BASE}/export-student-reports/${batchId}`, responseType: 'blob' });
  }

  // ── 报告留档 ──
  /** 生成并归档报告文件（type=class|students|excel） */
  archiveReport(batchId: string, type: 'class' | 'students' | 'excel' = 'class'): Observable<AnalysisReportFile> {
    return this.api<AnalysisReportFile>({
      method: 'POST',
      url: `${BASE}/archive-report/${batchId}`,
      params: { type },
    });
  }

  getReportFiles(batchId: string): Observable<AnalysisReportFile[]> {
    return this.api<AnalysisReportFile[]>({ method: 'GET', url: `${BASE}/report-files/${batchId}` }).pipe(
      catchError(() => of([] as AnalysisReportFile[]))
    );
  }

  downloadReportFile(fileId: string): Observable<Blob> {
    return this.api<Blob>({ method: 'POST', url: `${BASE}/download-report-file/${fileId}`, responseType: 'blob' });
  }

  deleteReportFile(fileId: string): Observable<void> {
    return this.api<void>({ method: 'DELETE', url: `${BASE}/report-file/${fileId}` });
  }

  /** 当前生效的推荐权重 */
  getRecommendationWeights(): Observable<RecommendationWeights> {
    return this.api<RecommendationWeights>({ method: 'GET', url: `${BASE}/recommendation-weights` });
  }

  /** 应用指定权重（写入 Setting，保存即生效） */
  applyRecommendationWeights(input: { content: number; collab: number; fit: number }): Observable<RecommendationWeights> {
    return this.api<RecommendationWeights>({ method: 'POST', url: `${BASE}/apply-recommendation-weights`, body: input });
  }

  /** 跑一次离线评估并应用最优权重 */
  applyBestRecommendationWeights(courseId?: string, topK = 5): Observable<RecommendationWeights> {
    return this.api<RecommendationWeights>({
      method: 'POST',
      url: `${BASE}/apply-best-recommendation-weights`,
      params: { courseId, topK },
    });
  }

  /** OCR 能力：图片是否可用、是否支持 PDF 直传 */
  getOcrCapabilities(): Observable<{ image: boolean; pdfDirect: boolean; pdfModel: string; note?: string | null }> {
    return this.api({ method: 'GET', url: `${BASE}/ocr-capabilities` });
  }

  /** 上传任意文件（如整份 PDF）到 OSS，返回公网 URL */
  uploadFileToOss(file: Blob, fileName: string): Observable<{ url: string; objectKey: string }> {
    const form = new FormData();
    form.append('file', file, fileName);
    return this.restService.request(
      { method: 'POST', url: '/api/oss-upload/file', body: form },
      { apiName: this.apiName }
    ) as Observable<{ url: string; objectKey: string }>;
  }

  /** 上传图片到 OSS，返回公网 URL（用于试卷图片直传，避免整班 Base64 撑大请求体） */
  uploadImageToOss(file: Blob, fileName: string): Observable<{ url: string; objectKey: string }> {
    const form = new FormData();
    form.append('file', file, fileName);
    return this.restService.request(
      { method: 'POST', url: '/api/oss-upload/image', body: form },
      { apiName: this.apiName }
    ) as Observable<{ url: string; objectKey: string }>;
  }

  /** 推荐效果离线评估（留出最近 K 条作答做测试集） */
  evaluateRecommendation(courseId?: string, topK = 5): Observable<RecommendationEvaluation> {
    return this.api<RecommendationEvaluation>({
      method: 'POST',
      url: `${BASE}/evaluate-recommendation`,
      params: { courseId, topK },
    });
  }

  /** 预览推送给某个学生的报告（student 为空取重点关注名单首个） */
  getStudentReportPreview(batchId: string, student?: string): Observable<StudentReportPreview> {
    return this.api<StudentReportPreview>({
      method: 'GET',
      url: `${BASE}/student-report-preview/${batchId}`,
      params: student ? { student } : undefined,
    });
  }

  // ════════════════════════════════════════════════════════
  //  教师端 · 分析
  // ════════════════════════════════════════════════════════

  getClassWeakness(batchId: string): Observable<ClassWeaknessDto> {
    return this.apiOrMock<ClassWeaknessDto>(
      { method: 'GET', url: `${BASE}/class-weakness/${batchId}` },
      MOCK_CLASS_WEAKNESS,
      '班级薄弱点'
    );
  }

  getTeachingInsight(batchId: string, useAiAdvice = true): Observable<TeachingInsightDto> {
    return this.apiOrMock<TeachingInsightDto>(
      {
        method: 'GET',
        url: `${BASE}/teaching-insight/${batchId}`,
        // useAiAdvice=false 时后端不调模型（导出场景），优先复用已持久化的建议
        params: useAiAdvice ? undefined : { useAiAdvice: false },
      },
      MOCK_TEACHING_INSIGHT,
      '教学成果分析'
    );
  }

  // ════════════════════════════════════════════════════════
  //  教师端 · 课程联动
  // ════════════════════════════════════════════════════════

  getCourseLink(courseId: string): Observable<CourseLinkConfig> {
    return this.apiOrMock<CourseLinkConfig>(
      { method: 'GET', url: `${BASE}/course-link/${courseId}` },
      { ...MOCK_COURSE_LINK, courseId },
      '课程联动配置'
    );
  }

  savePrerequisite(input: Partial<PrerequisiteMapping> & { courseId: string }): Observable<PrerequisiteMapping> {
    return this.api<PrerequisiteMapping>({ method: 'POST', url: `${BASE}/prerequisite`, body: input }).pipe(
      catchError(() =>
        of({
          ...(input as PrerequisiteMapping),
          id: input.id || `local-${Date.now()}`,
          status: input.status ?? 0,
        } as PrerequisiteMapping).pipe(delay(240))
      )
    );
  }

  deletePrerequisite(id: string): Observable<void> {
    return this.api<void>({ method: 'DELETE', url: `${BASE}/prerequisite/${id}` }).pipe(
      catchError(() => of(void 0).pipe(delay(160)))
    );
  }

  // ════════════════════════════════════════════════════════
  //  试卷图片上传 / OCR（真实）
  // ════════════════════════════════════════════════════════

  /** 单个批次详情（含 OCR 进度计数），用于轮询 */
  getPaperBatch(batchId: string): Observable<PaperBatchItem> {
    return this.api<PaperBatchItem>({ method: 'GET', url: `${BASE}/paper-batch/${batchId}` }).pipe(
      catchError(() => of(null as unknown as PaperBatchItem))
    );
  }

  /** 上传试卷图片（Base64 JSON；一张 = 一份学生试卷） */
  uploadPaperImages(batchId: string, files: PaperUploadPayload[]): Observable<void> {
    return this.api<void>({
      method: 'POST',
      url: `${BASE}/upload-paper-images/${batchId}`,
      body: { files },
    });
  }

  getPaperUploadImages(batchId: string): Observable<PaperUploadImage[]> {
    return this.api<PaperUploadImage[]>({
      method: 'GET',
      url: `${BASE}/paper-upload-images/${batchId}`,
    }).pipe(catchError(() => of([] as PaperUploadImage[])));
  }

  // ════════════════════════════════════════════════════════
  //  课程前后关系（CourseLink）
  // ════════════════════════════════════════════════════════

  saveCourseLink(input: SaveCourseLinkInput): Observable<CourseLinkItem> {
    return this.api<CourseLinkItem>({ method: 'POST', url: `${BASE}/save-course-link`, body: input });
  }

  deleteCourseLink(courseLinkId: string): Observable<void> {
    return this.api<void>({ method: 'DELETE', url: `${BASE}/course-link/${courseLinkId}` }).pipe(
      catchError(() => of(void 0))
    );
  }

  // ════════════════════════════════════════════════════════
  //  AI 任务（知识点抽取 / 模型训练 / 先修挖掘）
  // ════════════════════════════════════════════════════════

  /** 用 LLM 按课程抽取习题知识点（后台任务） */
  extractKnowledgePoints(courseId: string, overwrite = false): Observable<ExtractKnowledgePointsResult> {
    return this.api<ExtractKnowledgePointsResult>({
      method: 'POST',
      url: `${BASE}/extract-knowledge-points/${courseId}`,
      params: { overwrite },
    });
  }

  /** 重训掌握度矩阵分解模型 */
  /** 判定线：knowledgePointId 为空取课程级，否则取该知识点生效值（含是否覆盖） */
  getThreshold(courseId: string, knowledgePointId?: string): Observable<MasteryThreshold> {
    return this.api<MasteryThreshold>({
      method: 'GET',
      url: `${BASE}/threshold/${courseId}`,
      // 注意：后端参数名为 knowledgePoint（不带 Id 后缀），否则 ABP 会丢路径段
      params: knowledgePointId ? { knowledgePoint: knowledgePointId } : undefined,
    }).pipe(
      catchError(() =>
        of({ courseId, weakLine: 0.6, okLine: 0.75, isDefault: true, isOverride: false } as MasteryThreshold)
      )
    );
  }

  setThreshold(input: {
    courseId: string;
    knowledgePointId?: string | null;
    weakLine: number;
    okLine: number;
  }): Observable<void> {
    return this.api<void>({ method: 'POST', url: `${BASE}/set-threshold`, body: input });
  }

  /** 清除知识点级判定线覆盖（恢复课程级） */
  clearThreshold(input: { courseId: string; knowledgePointId: string }): Observable<void> {
    return this.api<void>({ method: 'POST', url: `${BASE}/clear-threshold`, body: input });
  }

  trainMasteryModel(): Observable<ModelTrainResult> {
    return this.api<ModelTrainResult>({ method: 'POST', url: `${BASE}/train-mastery-model` });
  }

  /** 立即生成并推送开课前衔接复习包（仅对已进入推送窗口的课程生效） */
  pushPreCourseReviews(): Observable<{ pushedCount: number }> {
    return this.api<{ pushedCount: number }>({ method: 'POST', url: `${BASE}/push-pre-course-reviews` });
  }

  /** 挖掘先修知识点候选 */
  minePrerequisites(courseId?: string): Observable<MiningResult> {
    return this.api<MiningResult>({
      method: 'POST',
      url: `${BASE}/mine-prerequisites`,
      params: courseId ? { courseId } : undefined,
    });
  }
}
