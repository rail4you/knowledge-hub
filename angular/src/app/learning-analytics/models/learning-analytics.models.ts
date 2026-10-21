/**
 * 智学分析（Learning Analytics）前端数据模型
 *
 * 与后端 `KnowledgeHub.Application.Contracts/LearningAnalytics/Dtos` 保持字段一致，
 * 阶段 A 由 Mock 服务填充，阶段 B/C 切换为真实 ABP 代理后无需改动组件。
 */

/** 掌握度等级：与后端 Level 枚举一致 */
export enum MasteryLevel {
  Weak = 0,
  Fair = 1,
  Ok = 2,
}

/** 知识点掌握项（条形图一行） */
export interface KnowledgeMasteryItem {
  knowledgePointId: string;
  /** 知识点名称 */
  name: string;
  /** 所属章节标题（教学达成度用） */
  chapterTitle?: string;
  /** 掌握度 0~1 */
  mastery: number;
  /** 有效作答量，太小表示「数据不足」 */
  evidence: number;
  /** 0 薄弱 / 1 待巩固 / 2 达标 */
  level: MasteryLevel;
  /** 是否为预测值（矩阵补全，展示时标注） */
  isPredicted?: boolean;
}

/** 失分归因（学生端「失分题目归因」） */
export interface LossAttribution {
  questionNo: string;
  exerciseId?: string;
  exerciseTitle: string;
  knowledgePointName: string;
  mastery: number;
  /** 归因结论，如「公式理解不足」 */
  reason: string;
  /** 建议，如「回看 5.2 平面图形面积」 */
  advice: string;
}

/** 学生薄弱点总览 */
export interface StudentWeaknessOverview {
  studentName: string;
  courseId: string;
  courseName: string;
  paperBatchId?: string;
  paperTitle: string;
  /** 期末成绩 */
  score?: number;
  classAverage?: number;
  classRank?: number;
  classSize?: number;
  /** 老师是否已确认（纸质试卷来源时） */
  confirmed: boolean;
  /** 来源标签，如「来自纸质试卷 · 老师已确认」 */
  sourceLabel: string;
  weakCount: number;
  knowledgePoints: KnowledgeMasteryItem[];
  losses: LossAttribution[];
}

/** 课程学习路径节点 */
export interface CoursePathNode {
  courseId: string;
  courseTitle: string;
  /** 0 已完成 / 1 进行中(即将开课) / 2 待学习 */
  state: 0 | 1 | 2;
  tag: string;
  note?: string;
  /** 需要衔接复习的前置知识点名 */
  prerequisiteNames?: string[];
}

/** 衔接复习进度 */
export interface ReviewPackageProgress {
  reviewPackageId: string;
  upcomingCourseId: string;
  upcomingCourseTitle: string;
  completed: number;
  total: number;
  /** 复习条目 */
  items: ReviewPackageItem[];
}

export interface ReviewPackageItem {
  knowledgePointId: string;
  name: string;
  /** 所属前置课程 */
  courseId: string;
  courseTitle: string;
  mastery: number;
  level: MasteryLevel;
  isCompleted: boolean;
}

/** 站内消息 */
export interface AnalysisNotification {
  id: string;
  type: string;
  title: string;
  content: string;
  source: string;
  createdAt: string;
  isRead: boolean;
}

/** 专项练习条目 */
export interface PracticeItem {
  id: string;
  title: string;
  courseId: string;
  courseTitle: string;
  exerciseCount: number;
  knowledgePointId?: string;
  /** 是否来自前置课程衔接 */
  fromPrerequisite: boolean;
  /** 推荐理由（可解释性） */
  reason?: string;
}

/** 学生端整页聚合数据 */
export interface StudentAnalysisDashboard {
  overview: StudentWeaknessOverview;
  path: CoursePathNode[];
  reviewProgress: ReviewPackageProgress | null;
  notifications: AnalysisNotification[];
  unreadCount: number;
  practices: PracticeItem[];
}

// ══════════════════════════════════════════════════════════
//  教师端
// ══════════════════════════════════════════════════════════

/** 试卷批次状态：0 草稿/待识别 1 识别中 2 待校对 3 已确认 4 已推送 */
export enum PaperBatchStatus {
  Draft = 0,
  OcrRunning = 1,
  NeedConfirm = 2,
  Confirmed = 3,
  Pushed = 4,
}

export interface PaperBatchItem {
  id: string;
  title: string;
  courseId: string;
  courseName: string;
  examDate: string;
  scriptCount: number;
  needsConfirmCount: number;
  pushedCount: number;
  averageScore: number;
  status: PaperBatchStatus;
  /** 已上传图片数 */
  uploadedCount: number;
  /** OCR 已处理图片数 */
  processedCount: number;
  /** OCR 失败图片数 */
  failedCount: number;
}

/** 试卷图片处理状态：0 待处理 / 1 处理中 / 2 完成 / 3 失败 */
export enum PaperUploadStatus {
  Pending = 0,
  Processing = 1,
  Done = 2,
  Failed = 3,
}

/** 试卷图片（一份学生试卷 = 一张图片，或一份多页 PDF 拆页后的若干张） */
export interface PaperUploadImage {
  id: string;
  fileName: string;
  sizeBytes: number;
  /** PDF 拆页页码（从 1 开始） */
  pageIndex: number;
  /** 多页合并分组键（同一份试卷的多个页面共享） */
  mergeGroup?: string | null;
  /** 所属学生（教师上传时指定） */
  studentId?: string | null;
  /** 所属学生姓名 */
  studentName: string;
  /** 所属学生学号 */
  studentNumber: string;
  status: PaperUploadStatus;
  errorMessage?: string | null;
  scriptId?: string | null;
}

/** 批次内已上传试卷的学生（按学生聚合的上传进度） */
export interface BatchStudentRow {
  studentId: string;
  studentName: string;
  studentNumber: string;
  /** 该学生已上传的文件数（多页 PDF 会 >1） */
  fileCount: number;
  /** 0 待处理 / 1 处理中 / 2 完成 / 3 失败 */
  status: PaperUploadStatus;
  errorMessage?: string | null;
}

/** 课程的选课学生（上传纸质试卷时选择归属学生） */
export interface CourseStudentDto {
  studentId: string;
  studentName: string;
  studentNumber: string;
  userName: string;
  /** 0 已选 / 1 学习中 / 2 已完成 / 3 已退课 */
  status: number;
}

/** 待上传图片（前端读取为 Base64） */
export interface PaperUploadPayload {
  fileName: string;
  contentType: string;
  /** 内联 Base64（未启用 OSS 时使用） */
  base64?: string;
  /** OSS 公网 URL（启用 OSS 直传时使用，后端不再接收 Base64） */
  url?: string;
  /** PDF 拆页页码 */
  pageIndex?: number;
  /** 多页合并分组键 */
  mergeGroup?: string;
}

/** 课程前后关系 */
export interface CourseLinkItem {
  id: string;
  predecessorCourseId: string;
  predecessorCourseTitle: string;
  successorCourseId: string;
  successorCourseTitle: string;
}

export interface SaveCourseLinkInput {
  predecessorCourseId: string;
  successorCourseId: string;
}

/** 课程掌握度判定线（0~1） */
export interface MasteryThreshold {
  id?: string | null;
  courseId: string;
  knowledgePointId?: string | null;
  weakLine: number;
  okLine: number;
  /** 未做任何显式配置（使用系统默认） */
  isDefault: boolean;
  /** 知识点级是否有覆盖 */
  isOverride?: boolean;
  courseWeakLine?: number | null;
  courseOkLine?: number | null;
}

/** 掌握度模型训练结果 */
export interface ModelTrainResult {
  trained: boolean;
  sampleCount: number;
  studentCount: number;
  knowledgePointCount: number;
  estimator: string;
  message: string;
}

/** 先修关系挖掘结果 */
export interface MiningResult {
  proposedCount: number;
  samplePairs: number;
  message: string;
}

/** 知识点抽取提交结果 */
export interface ExtractKnowledgePointsResult {
  courseId: string;
  exerciseCount: number;
  queued: boolean;
  message: string;
}

/** 教师端试卷分析概览 */
export interface PaperAnalysisOverview {
  batchCount: number;
  scriptCount: number;
  averageScore: number;
  pendingConfirmCount: number;
  pushedCount: number;
  batches: PaperBatchItem[];
}

/** Mock OCR 流水线步骤 */
export interface OcrPipelineStep {
  key: string;
  label: string;
  status: 'pending' | 'running' | 'done';
}

/** 单份试卷的题目识别行（校对表） */
export interface PaperItemRow {
  id: string;
  questionNo: string;
  /** 匹配到的应用内真实习题 */
  exerciseId?: string;
  exerciseTitle: string;
  matchStatus: 'matched' | 'low' | 'none';
  score: number;
  fullScore: number;
  knowledgePointNames: string[];
  ocrConfidence: number;
}

/** 待校对试卷 */
export interface PaperScriptRow {
  id: string;
  studentId?: string;
  studentName: string;
  studentNumber: string;
  totalScore: number;
  ocrConfidence: number;
  /** 0 待确认 1 已确认 */
  confirmed: boolean;
  items: PaperItemRow[];
}

/** 班级薄弱点 */
export interface ClassWeaknessDto {
  batchId: string;
  batchTitle: string;
  courseId: string;
  courseName: string;
  targetLine: number;
  knowledgePoints: KnowledgeMasteryItem[];
  focusStudents: FocusStudent[];
  pushedCount: number;
  totalStudents: number;
}

export interface FocusStudent {
  studentId: string;
  studentName: string;
  totalScore: number;
  weakestNames: string[];
  pushed: boolean;
}

/** 章节教学达成度 */
export interface ChapterStat {
  chapterId?: string;
  title: string;
  rate: number;
  stdDev: number;
  topQuartile: number;
  sampleSize: number;
}

/** 教学诊断结论 */
export interface TeachingDiagnosis {
  level: 'good' | 'fair' | 'gap' | 'polarized';
  chapterTitle: string;
  rate: number;
  /** 相对教学目标的百分点差 */
  gap: number;
  advice: string;
}

/** 教学成果分析 */
export interface TeachingInsightDto {
  batchId: string;
  courseId: string;
  courseName: string;
  /** 班级总得分率 0~1 */
  averageRate: number;
  /** 较上一届变化（百分点） */
  deltaVsPrevious: number;
  belowTargetCount: number;
  targetLine: number;
  chapters: ChapterStat[];
  diagnoses: TeachingDiagnosis[];
  /** 生成时间 */
  generatedAt: string;
}

/** 前置知识点映射（课程联动配置） */
export interface PrerequisiteMapping {
  id: string;
  /** 后续课程章节 */
  successorChapterTitle: string;
  /** 依赖的前置知识点 */
  dependencyNames: string[];
  /** 来源课程 */
  sourceCourseTitle: string;
  /** 触发条件描述 */
  condition: string;
  /** 0 待确认 / 1 已生效 */
  status: 0 | 1;
  /** 来源：Manual / Mined / Llm */
  source: string;
}

/** 课程联动配置 */
export interface CourseLinkConfig {
  courseId: string;
  courseName: string;
  path: CoursePathNode[];
  mappings: PrerequisiteMapping[];
  /** 已配置的课程前后关系 */
  links: CourseLinkItem[];
}

/** 上传单份学生试卷：一次一份卷，必须指定归属学生（课程的选课学生） */
export interface UploadPaperScriptInput {
  courseId: string;
  courseName: string;
  /** 试卷批次名称（同一课程 + 名称 + 考试日期视为同一批次） */
  title: string;
  examDate: string;
  studentId: string;
  studentName: string;
  studentNumber: string;
  /** 试卷文件：一张图片，或一份多页 PDF 拆页后的若干页 */
  files: PaperUploadPayload[];
}

/** 单份试卷上传结果 */
export interface UploadPaperScriptResult {
  batchId: string;
  batchTitle: string;
  imageId: string;
  studentId: string;
  studentName: string;
}

/** 学生报告推送预览（教师端「预览学生收到的推送」） */
export interface StudentReportPreview {
  studentId?: string | null;
  studentName: string;
  studentNumber: string;
  title: string;
  content: string;
  items: LossAttribution[];
  pushed: boolean;
  pushedAt?: string | null;
}

/** 归档报告文件 */
export interface AnalysisReportFile {
  id: string;
  batchId: string;
  /** 0 班级Word / 1 学生汇总Word / 2 学生明细Excel */
  type: number;
  typeLabel: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  /** 存储后端：oss / local */
  storage: string;
  downloadCount: number;
  creationTime: string;
  lastDownloadedAt?: string | null;
  metricsSnapshot?: string | null;
}

/** 当前生效的推荐权重 */
export interface RecommendationWeights {
  content: number;
  collab: number;
  fit: number;
  label: string;
  /** setting（已应用）/ config（appsettings 默认） */
  source: string;
}

/** 推荐权重评估结果 */
export interface RecommendationWeightSet {
  name: string;
  content: number;
  collab: number;
  fit: number;
  label: string;
  weakHitRate: number;
  precision: number;
  hits: number;
  weakTotal: number;
}

export interface RecommendationEvaluation {
  courseId?: string | null;
  /** 可评估学生数（测试集含错题） */
  studentCount: number;
  /** 实际处理过的学生数 */
  evaluatedStudentCount: number;
  answerCount: number;
  topK: number;
  results: RecommendationWeightSet[];
  best?: RecommendationWeightSet | null;
  current?: RecommendationWeightSet | null;
  message: string;
}
