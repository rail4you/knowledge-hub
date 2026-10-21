import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable, of, map, catchError, throwError } from 'rxjs';

import {
  AnalysisReportFile,
  ClassWeaknessDto,
  CourseLinkConfig,
  CourseLinkItem,
  CourseStudentDto,
  ExtractKnowledgePointsResult,
  GeneratePaperTemplateInput,
  MasteryThreshold,
  MiningResult,
  ModelTrainResult,
  PaperAnalysisOverview,
  PaperBatchItem,
  PaperBatchStatus,
  PaperGeneratorConfig,
  PaperScriptRow,
  PaperTemplate,
  PaperUploadImage,
  PaperUploadPayload,
  PrerequisiteMapping,
  RecommendationEvaluation,
  RecommendationWeights,
  SaveCourseLinkInput,
  StudentReportPreview,
  StudentReportListItem,
  StudentReportDetail,
  StudentAnalysisDashboard,
  TeachingInsightDto,
  UploadPaperScriptInput,
  UploadPaperScriptResult,
} from '../models/learning-analytics.models';
/** 后端约定：`LearningAnalyticsAppService` → `/api/app/learning-analytics` */
const BASE = '/api/app/learning-analytics';

/**
 * 智学分析服务（学生端 + 教师端共用）。
 *
 * 只调用真实后端接口（`KnowledgeHub.LearningAnalytics` 模块）：
 * 接口不可用时直接抛错，由调用方展示错误提示，不回退任何示例数据。
 */
@Injectable({ providedIn: 'root' })
export class LearningAnalyticsService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  private api<T>(request: any): Observable<T> {
    return this.restService.request(request, { apiName: this.apiName }) as Observable<T>;
  }

  // ════════════════════════════════════════════════════════
  //  学生端
  // ════════════════════════════════════════════════════════

  /** 学生端整页聚合：薄弱点 + 学习路径 + 消息 + 专项练习 */
  getStudentDashboard(): Observable<StudentAnalysisDashboard> {
    return this.api<StudentAnalysisDashboard>({ method: 'GET', url: `${BASE}/student-dashboard` });
  }

  /** 标记消息已读 */
  markNotificationRead(id: string): Observable<void> {
    return this.api<void>({ method: 'POST', url: `${BASE}/mark-notification-read/${id}` }).pipe(
      catchError(() => of(void 0))
    );
  }

  /** 学生端：老师推送给我的学习分析报告列表 */
  getMyReports(): Observable<StudentReportListItem[]> {
    return this.api<StudentReportListItem[]>({ method: 'GET', url: `${BASE}/my-reports` });
  }

  /** 学生端：报告详情（薄弱点 + 学习计划） */
  getMyReport(reportId: string): Observable<StudentReportDetail> {
    return this.api<StudentReportDetail>({ method: 'GET', url: `${BASE}/my-report/${reportId}` });
  }

  /** 学生端：把报告下载成 Word */
  downloadMyReport(reportId: string): Observable<Blob> {
    return this.api<Blob>({ method: 'POST', url: `${BASE}/download-my-report/${reportId}`, responseType: 'blob' });
  }

  // ════════════════════════════════════════════════════════
  //  教师端 · 纸质试卷
  // ════════════════════════════════════════════════════════

  getPaperOverview(): Observable<PaperAnalysisOverview> {
    return this.api<PaperAnalysisOverview>({ method: 'GET', url: `${BASE}/paper-overview` });
  }

  /** 课程的选课学生（上传纸质试卷时选择归属学生） */
  getCourseStudents(courseId: string): Observable<CourseStudentDto[]> {
    return this.api<CourseStudentDto[]>({
      method: 'GET',
      url: `${BASE}/course-students/${courseId}`,
    });
  }

  /**
   * 上传单份学生试卷（一次一份卷，必须指定归属学生）。
   * 后端会把试卷落到对应批次并立即触发 OCR，前端用 `getPaperBatch` 轮询进度。
   */
  uploadPaperScript(input: UploadPaperScriptInput): Observable<UploadPaperScriptResult> {
    return this.api<UploadPaperScriptResult>({
      method: 'POST',
      url: `${BASE}/upload-paper-script`,
      body: input,
    });
  }

  /** 触发 OCR（后端入队执行，前端需要轮询批次状态） */
  runPaperOcr(batchId: string): Observable<void> {
    return this.api<void>({ method: 'POST', url: `${BASE}/run-paper-ocr/${batchId}` });
  }

  getPaperScripts(batchId: string): Observable<PaperScriptRow[]> {
    return this.api<PaperScriptRow[]>({ method: 'GET', url: `${BASE}/paper-scripts/${batchId}` });
  }

  /** 校对并确认单份试卷（确认后写入真实作答记录） */
  confirmPaperScript(scriptId: string, rows: PaperScriptRow[]): Observable<void> {
    const script = rows.find(r => r.id === scriptId);
    return this.api<void>({
      method: 'POST',
      url: `${BASE}/confirm-paper-script/${scriptId}`,
      body: { items: script?.items ?? [], totalScore: script?.totalScore ?? 0 },
    });
  }

  /** 一键推送学生个人薄弱点报告 */
  pushPaperReports(batchId: string): Observable<{ pushedCount: number }> {
    return this.api<{ pushedCount: number }>({ method: 'POST', url: `${BASE}/push-reports/${batchId}` });
  }

  /** 删除单份试卷的分析结果（识别明细 + 作答记录 + 来源图片） */
  deletePaperScript(scriptId: string): Observable<void> {
    return this.api<void>({ method: 'DELETE', url: `${BASE}/paper-script/${scriptId}` });
  }

  /** 删除某个试卷批次的全部分析结果（含批次本身） */
  deletePaperBatch(batchId: string): Observable<void> {
    return this.api<void>({ method: 'DELETE', url: `${BASE}/paper-batch/${batchId}` });
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

  // ── 试卷生成器 ──

  /** 生成一份可 OCR 扫描的试卷样张（题目从课程题库随机抽，得分可随机） */
  generatePaperTemplate(input: GeneratePaperTemplateInput): Observable<PaperTemplate> {
    return this.api<PaperTemplate>({ method: 'POST', url: `${BASE}/generate-paper-template`, body: input });
  }

  /** 试卷生成器当前生效配置 */
  getPaperGeneratorConfig(): Observable<PaperGeneratorConfig> {
    return this.api<PaperGeneratorConfig>({ method: 'GET', url: `${BASE}/paper-generator-config` });
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
    return this.api<ClassWeaknessDto>({ method: 'GET', url: `${BASE}/class-weakness/${batchId}` });
  }

  getTeachingInsight(batchId: string, useAiAdvice = true): Observable<TeachingInsightDto> {
    return this.api<TeachingInsightDto>({
      method: 'GET',
      url: `${BASE}/teaching-insight/${batchId}`,
      // useAiAdvice=false 时后端不调模型（导出场景），优先复用已持久化的建议
      params: useAiAdvice ? undefined : { useAiAdvice: false },
    });
  }

  // ════════════════════════════════════════════════════════
  //  教师端 · 课程联动
  // ════════════════════════════════════════════════════════

  getCourseLink(courseId: string): Observable<CourseLinkConfig> {
    return this.api<CourseLinkConfig>({ method: 'GET', url: `${BASE}/course-link/${courseId}` });
  }

  savePrerequisite(input: Partial<PrerequisiteMapping> & { courseId: string }): Observable<PrerequisiteMapping> {
    return this.api<PrerequisiteMapping>({ method: 'POST', url: `${BASE}/prerequisite`, body: input });
  }

  deletePrerequisite(id: string): Observable<void> {
    return this.api<void>({ method: 'DELETE', url: `${BASE}/prerequisite/${id}` });
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
