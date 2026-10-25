import { MasteryLevel } from '../../learning-analytics/models/learning-analytics.models';

/**
 * 「智学分析」页签与标签表。
 *
 * 规范要求：硬编码中文标签集中放在模块自己的 `*-labels.ts`，
 * 组件里只引用这份表，避免标签在模板与逻辑之间漂移。
 */

/** 学生端智学分析的页签 */
export type AnalysisTab = 'weakness' | 'path' | 'messages' | 'practice';

/** 页签定义（顺序即展示顺序） */
export const ANALYSIS_TABS: { key: AnalysisTab; label: string; icon: string }[] = [
  { key: 'weakness', label: '我的薄弱点', icon: 'exclamation-circle' },
  { key: 'path', label: '学习路径', icon: 'deployment-unit' },
  { key: 'messages', label: '学习消息', icon: 'bell' },
  { key: 'practice', label: '专项练习', icon: 'edit' },
];

/** 后端掌握度等级（MasteryGrade：0 薄弱 / 1 待巩固 / 2 达标） */
export const MASTERY_LEVEL_LABELS: Record<MasteryLevel, string> = {
  [MasteryLevel.Weak]: '薄弱',
  [MasteryLevel.Fair]: '待巩固',
  [MasteryLevel.Ok]: '达标',
};

/**
 * 掌握度判定线的兜底常量。
 *
 * 取值优先级：
 *   1. 知识点项上的 `targetLine`（后端按知识点 / 课程解析后的生效值）
 *   2. `GET /api/app/learning-analytics/threshold/{courseId}` 返回的 `okLine`
 *   3. 这里兜底（与后端 `Thresholds.Default` 保持一致：0.75 达标 / 0.60 薄弱）
 */
export const DEFAULT_MASTERY_OK_LINE = 0.75;
export const DEFAULT_MASTERY_WEAK_LINE = 0.6;

/** 纸质试卷来源的确认状态文案 */
export const PAPER_CONFIRM_LABELS = {
  confirmed: '老师已确认',
  pending: '待老师确认',
} as const;

/** 学生端空态文案（与原型 learner-analysis 的说明保持一致） */
export const EMPTY_TEXT = {
  noConfirmedPaper: '暂无教师已确认的试卷记录。未经校对的识别结果不会计入。',
  noWeakness: '还没有可分析的学情数据。完成课程练习，或等待老师确认纸质试卷后即可查看。',
  noMessage: '暂无新消息。老师发布试卷反馈或复习提醒后会出现在这里。',
  noPractice: '暂无推荐练习。先完成一次课程练习，或等待老师确认试卷后即可获得推荐。',
  noPath: '暂无课程学习路径数据。选课并开始学习后，这里会显示课程顺序与先修提示。',
} as const;
