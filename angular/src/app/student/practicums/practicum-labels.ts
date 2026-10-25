// ═══════════════════════════════════════════════════════════════
//  学生端 · 实训域枚举与状态映射（唯一来源）
// ───────────────────────────────────────────────────────────────
//  规范要求：枚举标签集中放在模块的 *-labels.ts，不散落在组件里。
//
//  关键约束：**未提交 / 待批阅 / 零分 是三个互不相同的状态**。
//  · 未提交 —— 该任务下没有任何提交记录（enrollment.status = Enrolled）
//  · 待批阅 —— 有提交，但尚未被教师批阅（submission.status = Submitted）
//  · 零分   —— 已被批阅且分数为 0（submission.score = 0 / enrollment.finalScore = 0）
//  「未评分」（score = null）与「零分」必须区分展示，不能都写成「未评分」。
// ═══════════════════════════════════════════════════════════════

import { PracticumEnrollmentStatus } from '../../proxy/practicums/enums/practicum-enrollment-status.enum';
import { PracticumSubmissionStatus } from '../../proxy/practicums/enums/practicum-submission-status.enum';
import type { PracticumEnrollmentDto, PracticumSubmissionDto } from '../../proxy/practicums/dtos/models';

/** 单个任务的成果状态（对齐原型 #practicum-workspace 的状态机） */
export type PracticumWorkState =
  | 'not-submitted' // 未提交
  | 'pending-review' // 待批阅
  | 'returned' // 待修改（退回）
  | 'reviewed'; // 已反馈

/** 项目整体成果状态（在任务状态之上再区分「推进中」与「已反馈」） */
export type PracticumProjectWorkState =
  | 'not-submitted' // 未提交
  | 'in-progress' // 推进中
  | 'pending-review' // 待批阅
  | 'returned' // 待修改（退回）
  | 'scored' // 已反馈
  | 'cancelled'; // 已取消

export const WORK_STATE_LABELS: Record<PracticumWorkState, string> = {
  'not-submitted': '未提交',
  'pending-review': '待批阅',
  returned: '待修改（退回）',
  reviewed: '已反馈',
};

export const PROJECT_WORK_STATE_LABELS: Record<PracticumProjectWorkState, string> = {
  'not-submitted': '未提交',
  'in-progress': '推进中',
  'pending-review': '待批阅',
  returned: '待修改（退回）',
  scored: '已反馈',
  cancelled: '已取消',
};

/** 状态色调：warn=待办 / info=进行中 / bad=需修改 / ok=已完成 */
export type WorkStateTone = 'warn' | 'info' | 'bad' | 'ok' | 'gray';

export const WORK_STATE_TONES: Record<PracticumWorkState, WorkStateTone> = {
  'not-submitted': 'warn',
  'pending-review': 'info',
  returned: 'bad',
  reviewed: 'ok',
};

export const PROJECT_WORK_STATE_TONES: Record<PracticumProjectWorkState, WorkStateTone> = {
  'not-submitted': 'warn',
  'in-progress': 'info',
  'pending-review': 'info',
  returned: 'bad',
  scored: 'ok',
  cancelled: 'gray',
};

/** 后端 enrollment.status 的中文标签（用于展示后端原始状态位） */
export const ENROLLMENT_STATUS_LABELS: Record<PracticumEnrollmentStatus, string> = {
  [PracticumEnrollmentStatus.Enrolled]: '未提交',
  [PracticumEnrollmentStatus.InProgress]: '推进中',
  [PracticumEnrollmentStatus.Submitted]: '待批阅',
  [PracticumEnrollmentStatus.Reviewed]: '已反馈',
  [PracticumEnrollmentStatus.Completed]: '已完成',
  [PracticumEnrollmentStatus.Cancelled]: '已取消',
};

/** 后端 submission.status 的中文标签 */
export const SUBMISSION_STATUS_LABELS: Record<PracticumSubmissionStatus, string> = {
  [PracticumSubmissionStatus.Submitted]: '待批阅',
  [PracticumSubmissionStatus.Returned]: '待修改（退回）',
  [PracticumSubmissionStatus.Reviewed]: '已反馈',
};

/** 单条提交记录 → 任务状态 */
export function workStateOf(submission?: PracticumSubmissionDto | null): PracticumWorkState {
  if (!submission) return 'not-submitted';
  switch (submission.status) {
    case PracticumSubmissionStatus.Submitted:
      return 'pending-review';
    case PracticumSubmissionStatus.Returned:
      return 'returned';
    case PracticumSubmissionStatus.Reviewed:
      return 'reviewed';
    default:
      return 'not-submitted';
  }
}

/**
 * 项目整体状态。
 *
 * 判定顺序刻意与后端 RefreshEnrollmentProgressAsync 保持一致：
 *   有退回 → 待修改；有未批阅 → 待批阅；全部任务已提交且已批阅 → 已反馈；
 *   有提交但仍有任务没交 → 推进中；一条都没交 → 未提交。
 */
export function projectWorkStateOf(
  enrollment: PracticumEnrollmentDto | null | undefined,
  submissions: PracticumSubmissionDto[],
  totalTasks: number,
): PracticumProjectWorkState {
  if (enrollment?.status === PracticumEnrollmentStatus.Cancelled) return 'cancelled';
  const list = submissions || [];
  if (list.some(s => s.status === PracticumSubmissionStatus.Returned)) return 'returned';
  if (list.some(s => s.status !== PracticumSubmissionStatus.Reviewed)) return 'pending-review';
  if (list.length === 0) return 'not-submitted';
  if (totalTasks > 0 && new Set(list.map(s => s.taskId)).size < totalTasks) return 'in-progress';
  return 'scored';
}

/** 成绩展示：未评分 / 0 分（零分）/ N 分 —— 三者文案与色调互不相同 */
export interface ScoreView {
  /** 展示文案 */
  text: string;
  /** 色调：none=未评分 / zero=零分 / normal=正常分数 */
  tone: 'none' | 'zero' | 'normal';
  /** 是否已评分（零分也算已评分） */
  scored: boolean;
}

export function scoreView(score: number | null | undefined): ScoreView {
  if (score === null || score === undefined) {
    return { text: '未评分', tone: 'none', scored: false };
  }
  if (score === 0) {
    return { text: '0 分', tone: 'zero', scored: true };
  }
  return { text: `${score} 分`, tone: 'normal', scored: true };
}

/** 「本次成果使用过 AI 辅助」的披露文案（后端提交 DTO 无独立字段，只能并入正文） */
export const AI_ASSIST_DISCLOSURE = '【AI 辅助声明】本次成果使用过 AI 辅助。';
