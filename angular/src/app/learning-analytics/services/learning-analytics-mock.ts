import {
  AnalysisNotification,
  ChapterStat,
  ClassWeaknessDto,
  CourseLinkConfig,
  CoursePathNode,
  FocusStudent,
  KnowledgeMasteryItem,
  MasteryLevel,
  PaperAnalysisOverview,
  PaperBatchItem,
  PaperBatchStatus,
  PaperScriptRow,
  PracticeItem,
  PrerequisiteMapping,
  ReviewPackageProgress,
  StudentAnalysisDashboard,
  StudentWeaknessOverview,
  TeachingInsightDto,
} from '../models/learning-analytics.models';

/**
 * 阶段 A 的 Mock 数据（仅用于 UI 原型定稿）。
 *
 * ⚠ 这些数据是**示例**，不是真实成绩。阶段 B 后端完成后，
 * `LearningAnalyticsService.useApi = true` 即切换为真实接口，本文件不再被引用。
 */

export const MOCK_STUDENT_NAME = '张晨';

export const MOCK_COURSE = { id: 'mock-course-calc', name: '高等数学B' };

function masteryLevel(mastery: number): MasteryLevel {
  if (mastery < 0.6) return MasteryLevel.Weak;
  if (mastery < 0.75) return MasteryLevel.Fair;
  return MasteryLevel.Ok;
}

function kp(name: string, mastery: number, chapterTitle?: string, evidence = 6): KnowledgeMasteryItem {
  return {
    knowledgePointId: `mock-kp-${name}`,
    name,
    chapterTitle,
    mastery,
    evidence,
    level: masteryLevel(mastery),
  };
}

export const MOCK_STUDENT_KPS: KnowledgeMasteryItem[] = [
  kp('级数收敛判别', 0.32, '第6章 无穷级数'),
  kp('定积分应用', 0.41, '第5章 定积分应用'),
  kp('多元函数微分', 0.55, '第7章 多元函数'),
  kp('微分方程', 0.66, '第8章 常微分方程'),
  kp('不定积分', 0.72, '第4章 不定积分'),
  kp('极限与连续', 0.84, '第1章 极限'),
  kp('导数与微分', 0.85, '第2章 导数'),
];

export const MOCK_OVERVIEW: StudentWeaknessOverview = {
  studentName: MOCK_STUDENT_NAME,
  courseId: MOCK_COURSE.id,
  courseName: MOCK_COURSE.name,
  paperBatchId: 'mock-batch-1',
  paperTitle: '2023秋 期末考试',
  score: 48,
  classAverage: 71.5,
  classRank: 30,
  classSize: 42,
  confirmed: true,
  sourceLabel: '来自纸质试卷 · 老师已确认',
  weakCount: MOCK_STUDENT_KPS.filter(k => k.level === MasteryLevel.Weak).length,
  knowledgePoints: MOCK_STUDENT_KPS,
  losses: [
    {
      questionNo: '5',
      exerciseTitle: '定积分求平面图形面积',
      knowledgePointName: '定积分应用',
      mastery: 0.41,
      reason: '公式理解不足',
      advice: '回看「5.2 平面图形面积」并完成 5 道同类题',
    },
    {
      questionNo: '9',
      exerciseTitle: '级数收敛性判断',
      knowledgePointName: '级数收敛判别',
      mastery: 0.32,
      reason: '判别法选择错误',
      advice: '完成「级数判别法」专项练习，重点比较比值/根值判别法',
    },
  ],
};

export const MOCK_PATH: CoursePathNode[] = [
  {
    courseId: MOCK_COURSE.id,
    courseTitle: '高等数学B',
    state: 0,
    tag: '已完成',
    note: '薄弱点 4 个 → 已生成复习包',
  },
  {
    courseId: 'mock-course-linalg',
    courseTitle: '线性代数',
    state: 1,
    tag: '下学期 · 2周后开课',
    note: '开课前建议先补：偏导数、多元函数微分',
    prerequisiteNames: ['偏导数', '多元函数微分'],
  },
  {
    courseId: 'mock-course-prob',
    courseTitle: '概率统计',
    state: 2,
    tag: '待学习',
  },
];

export const MOCK_REVIEW_PROGRESS: ReviewPackageProgress = {
  reviewPackageId: 'mock-pkg-1',
  upcomingCourseId: 'mock-course-linalg',
  upcomingCourseTitle: '线性代数',
  completed: 1,
  total: 4,
  items: [
    { knowledgePointId: 'mock-kp-1', name: '偏导数', courseId: MOCK_COURSE.id, courseTitle: '高等数学B', mastery: 0.58, level: MasteryLevel.Weak, isCompleted: false },
    { knowledgePointId: 'mock-kp-2', name: '多元函数微分', courseId: MOCK_COURSE.id, courseTitle: '高等数学B', mastery: 0.61, level: MasteryLevel.Fair, isCompleted: true },
    { knowledgePointId: 'mock-kp-3', name: '行列式', courseId: 'mock-course-linalg', courseTitle: '高等数学A', mastery: 0.64, level: MasteryLevel.Fair, isCompleted: false },
    { knowledgePointId: 'mock-kp-4', name: '多项式根与因式分解', courseId: 'mock-course-linalg', courseTitle: '高等数学A', mastery: 0.7, level: MasteryLevel.Fair, isCompleted: false },
  ],
};

export const MOCK_NOTIFICATIONS: AnalysisNotification[] = [
  {
    id: 'mock-n-1',
    type: 'PreCourseReview',
    title: '【课前提醒】线性代数即将开课',
    content:
      '你在《高等数学B》中「多元函数微分」掌握率仅 61%，该知识点是线性代数第 6 章的基础，已为你准备了 3 道衔接练习。',
    source: '系统',
    createdAt: new Date(Date.now() - 3600_000 * 5).toISOString(),
    isRead: false,
  },
  {
    id: 'mock-n-2',
    type: 'PaperWeakness',
    title: '【成绩分析】期末试卷薄弱点报告',
    content: '老师已上传并确认你的纸质试卷，已生成 4 个薄弱点，点击查看复习建议。',
    source: '李老师',
    createdAt: new Date(Date.now() - 86400_000).toISOString(),
    isRead: false,
  },
];

export const MOCK_PRACTICES: PracticeItem[] = [
  { id: 'mock-p-1', title: '级数判别法对比', courseId: MOCK_COURSE.id, courseTitle: '高等数学B', exerciseCount: 6, fromPrerequisite: false, reason: '级数收敛判别 掌握率 32%' },
  { id: 'mock-p-2', title: '定积分求面积', courseId: MOCK_COURSE.id, courseTitle: '高等数学B', exerciseCount: 5, fromPrerequisite: false, reason: '定积分应用 掌握率 41%' },
  { id: 'mock-p-3', title: '偏导数衔接练习', courseId: 'mock-course-linalg', courseTitle: '线性代数前置', exerciseCount: 3, fromPrerequisite: true, reason: '线性代数开课前衔接复习' },
];

export const MOCK_STUDENT_DASHBOARD: StudentAnalysisDashboard = {
  overview: MOCK_OVERVIEW,
  path: MOCK_PATH,
  reviewProgress: MOCK_REVIEW_PROGRESS,
  notifications: MOCK_NOTIFICATIONS,
  unreadCount: MOCK_NOTIFICATIONS.filter(n => !n.isRead).length,
  practices: MOCK_PRACTICES,
};

// ══════════════════════════════════════════════════════════
//  教师端
// ══════════════════════════════════════════════════════════

export const MOCK_BATCHES: PaperBatchItem[] = [
  {
    id: 'mock-batch-1',
    title: '2023秋 期末考试',
    courseId: MOCK_COURSE.id,
    courseName: MOCK_COURSE.name,
    examDate: '2024-01-10',
    scriptCount: 42,
    needsConfirmCount: 0,
    pushedCount: 42,
    averageScore: 71.5,
    status: PaperBatchStatus.Pushed,
    uploadedCount: 42,
    processedCount: 42,
    failedCount: 0,
  },
  {
    id: 'mock-batch-2',
    title: '2023秋 期中考试',
    courseId: MOCK_COURSE.id,
    courseName: MOCK_COURSE.name,
    examDate: '2023-11-08',
    scriptCount: 42,
    needsConfirmCount: 9,
    pushedCount: 0,
    averageScore: 68.2,
    status: PaperBatchStatus.NeedConfirm,
    uploadedCount: 42,
    processedCount: 33,
    failedCount: 0,
  },
];

export const MOCK_PAPER_OVERVIEW: PaperAnalysisOverview = {
  batchCount: MOCK_BATCHES.length,
  scriptCount: MOCK_BATCHES.reduce((s, b) => s + b.scriptCount, 0),
  averageScore: 71.5,
  pendingConfirmCount: 9,
  pushedCount: 42,
  batches: MOCK_BATCHES,
};

export const MOCK_FOCUS_STUDENTS: FocusStudent[] = [
  { studentId: 'mock-s-1', studentName: '张晨', totalScore: 48, weakestNames: ['定积分应用', '级数收敛判别'], pushed: true },
  { studentId: 'mock-s-2', studentName: '李思雨', totalScore: 62, weakestNames: ['多元函数微分'], pushed: true },
  { studentId: 'mock-s-3', studentName: '王浩', totalScore: 65, weakestNames: ['级数收敛判别'], pushed: false },
];

export const MOCK_CLASS_KPS: KnowledgeMasteryItem[] = [
  kp('级数收敛判别', 0.48, '第6章 无穷级数', 38),
  kp('定积分应用', 0.55, '第5章 定积分应用', 40),
  kp('多元函数微分', 0.61, '第7章 多元函数', 36),
  kp('微分方程', 0.68, '第8章 常微分方程', 34),
  kp('不定积分', 0.76, '第4章 不定积分', 41),
  kp('极限与连续', 0.84, '第1章 极限', 42),
  kp('导数与微分', 0.88, '第2章 导数', 42),
];

export const MOCK_CLASS_WEAKNESS: ClassWeaknessDto = {
  batchId: 'mock-batch-1',
  batchTitle: '2023秋 期末考试',
  courseId: MOCK_COURSE.id,
  courseName: MOCK_COURSE.name,
  targetLine: 0.75,
  knowledgePoints: MOCK_CLASS_KPS,
  focusStudents: MOCK_FOCUS_STUDENTS,
  pushedCount: 33,
  totalStudents: 42,
};

const chapter = (title: string, rate: number, stdDev: number, topQuartile: number): ChapterStat => ({
  title,
  rate,
  stdDev,
  topQuartile,
  sampleSize: 42,
});

export const MOCK_TEACHING_INSIGHT: TeachingInsightDto = {
  batchId: 'mock-batch-1',
  courseId: MOCK_COURSE.id,
  courseName: MOCK_COURSE.name,
  averageRate: 0.715,
  deltaVsPrevious: 0.032,
  belowTargetCount: 3,
  targetLine: 0.75,
  chapters: [
    chapter('第6章 无穷级数', 0.52, 0.21, 0.63),
    chapter('第5章 定积分应用', 0.58, 0.29, 0.79),
    chapter('第7章 多元函数', 0.64, 0.18, 0.77),
    chapter('第4章 不定积分', 0.77, 0.15, 0.9),
    chapter('第1章 极限', 0.83, 0.12, 0.94),
    chapter('第2章 导数', 0.88, 0.1, 0.96),
  ],
  diagnoses: [
    {
      level: 'gap',
      chapterTitle: '第6章 无穷级数',
      rate: 0.52,
      gap: -0.23,
      advice: '得分率 52%，低于目标 23 个百分点，且高分学生同样失分 → 倾向教学讲解不足，建议增加判别法对比讲解与 2 课时练习。',
    },
    {
      level: 'polarized',
      chapterTitle: '第5章 定积分应用',
      rate: 0.58,
      gap: -0.17,
      advice: '得分率 58%，两极分化明显（标准差 0.29）→ 建议分层布置课堂练习，对后 25% 学生补充基础模板题。',
    },
    {
      level: 'good',
      chapterTitle: '第2章 导数',
      rate: 0.88,
      gap: 0.13,
      advice: '得分率 88%，达成度高，可作为经验沉淀并复用到后续章节的教学设计。',
    },
  ],
  generatedAt: new Date().toISOString(),
};

export const MOCK_MAPPINGS: PrerequisiteMapping[] = [
  {
    id: 'mock-m-1',
    successorChapterTitle: '多元函数极值',
    dependencyNames: ['偏导数', '多元函数微分'],
    sourceCourseTitle: '高等数学B',
    condition: '掌握率 < 60%',
    status: 1,
    source: 'Manual',
  },
  {
    id: 'mock-m-2',
    successorChapterTitle: '矩阵特征值',
    dependencyNames: ['多项式根', '行列式'],
    sourceCourseTitle: '高等数学A',
    condition: '掌握率 < 60%',
    status: 0,
    source: 'Mined',
  },
];

export const MOCK_COURSE_LINK: CourseLinkConfig = {
  courseId: 'mock-course-linalg',
  courseName: '线性代数',
  path: MOCK_PATH,
  mappings: MOCK_MAPPINGS,
  links: [
    {
      id: 'mock-link-1',
      predecessorCourseId: MOCK_COURSE.id,
      predecessorCourseTitle: '高等数学B',
      successorCourseId: 'mock-course-linalg',
      successorCourseTitle: '线性代数',
    },
  ],
};

/** 待校对试卷的题目行（Mock OCR 输出） */
export const MOCK_SCRIPT_ROWS: PaperScriptRow[] = [
  {
    id: 'mock-script-1',
    studentId: 'mock-s-1',
    studentName: '张晨',
    studentNumber: '2023010101',
    totalScore: 48,
    ocrConfidence: 0.72,
    confirmed: false,
    items: [
      { id: 'i1', questionNo: '1', exerciseId: 'mock-ex-1', exerciseTitle: '极限与连续基础题', matchStatus: 'matched', score: 5, fullScore: 5, knowledgePointNames: ['极限与连续'], ocrConfidence: 0.96 },
      { id: 'i2', questionNo: '5', exerciseId: 'mock-ex-5', exerciseTitle: '定积分求平面图形面积', matchStatus: 'matched', score: 1, fullScore: 8, knowledgePointNames: ['定积分应用'], ocrConfidence: 0.83 },
      { id: 'i3', questionNo: '9', exerciseId: 'mock-ex-9', exerciseTitle: '级数收敛性判断', matchStatus: 'low', score: 2, fullScore: 10, knowledgePointNames: ['级数收敛判别'], ocrConfidence: 0.58 },
      { id: 'i4', questionNo: '12', exerciseTitle: '（未匹配到题库原题）', matchStatus: 'none', score: 0, fullScore: 6, knowledgePointNames: ['多元函数微分'], ocrConfidence: 0.41 },
    ],
  },
  {
    id: 'mock-script-2',
    studentId: 'mock-s-2',
    studentName: '李思雨',
    studentNumber: '2023010102',
    totalScore: 62,
    ocrConfidence: 0.91,
    confirmed: false,
    items: [
      { id: 'i5', questionNo: '1', exerciseId: 'mock-ex-1', exerciseTitle: '极限与连续基础题', matchStatus: 'matched', score: 5, fullScore: 5, knowledgePointNames: ['极限与连续'], ocrConfidence: 0.97 },
      { id: 'i6', questionNo: '5', exerciseId: 'mock-ex-5', exerciseTitle: '定积分求平面图形面积', matchStatus: 'matched', score: 6, fullScore: 8, knowledgePointNames: ['定积分应用'], ocrConfidence: 0.92 },
    ],
  },
];
