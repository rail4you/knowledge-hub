import {
  EmploymentApplicationStatus,
  EmploymentGuidanceSourceType,
  EmploymentJobType,
  EmploymentOutcomeStatus,
} from '../../proxy/employment/enums';

/**
 * 学生端就业模块的枚举文案与色调映射。
 *
 * 规范要求：枚举标签集中放在本模块的 `*-labels.ts`，不要散落到各组件；
 * 枚举值一律来自后端（生成代理的 enums），这里只负责中文标签与语义色调。
 *
 * 色调用语义词（tone），具体颜色在 SCSS 里用 var(--kh-*) 变量实现，
 * 组件里不出现任何硬编码色值。
 */

/** 语义色调：ok / info / warn / bad / muted */
export type EmploymentTone = 'ok' | 'info' | 'warn' | 'bad' | 'muted';

/** 岗位类型筛选项 */
export const JOB_TYPE_OPTIONS: { value: EmploymentJobType; label: string }[] = [
  { value: EmploymentJobType.FullTime, label: '全职' },
  { value: EmploymentJobType.Internship, label: '实习' },
  { value: EmploymentJobType.PartTime, label: '兼职' },
  { value: EmploymentJobType.Apprenticeship, label: '学徒' },
];

/** 投递状态筛选项（后端枚举 EmploymentApplicationStatus） */
export const APPLICATION_STATUS_OPTIONS: {
  value: EmploymentApplicationStatus;
  label: string;
}[] = [
  { value: EmploymentApplicationStatus.Submitted, label: '已投递' },
  { value: EmploymentApplicationStatus.Viewed, label: '已查看' },
  { value: EmploymentApplicationStatus.InterviewScheduled, label: '等待面试' },
  { value: EmploymentApplicationStatus.InterviewCompleted, label: '面试完成' },
  { value: EmploymentApplicationStatus.Offered, label: '已录用' },
  { value: EmploymentApplicationStatus.Rejected, label: '未通过' },
  { value: EmploymentApplicationStatus.Withdrawn, label: '已撤回' },
];

const APPLICATION_STATUS_LABELS: Record<number, string> = {
  [EmploymentApplicationStatus.Submitted]: '已投递',
  [EmploymentApplicationStatus.Viewed]: '已查看',
  [EmploymentApplicationStatus.InterviewScheduled]: '等待面试',
  [EmploymentApplicationStatus.InterviewCompleted]: '面试完成',
  [EmploymentApplicationStatus.Offered]: '已录用',
  [EmploymentApplicationStatus.Rejected]: '未通过',
  [EmploymentApplicationStatus.Withdrawn]: '已撤回',
};

const APPLICATION_STATUS_TONES: Record<number, EmploymentTone> = {
  [EmploymentApplicationStatus.Submitted]: 'info',
  [EmploymentApplicationStatus.Viewed]: 'info',
  [EmploymentApplicationStatus.InterviewScheduled]: 'warn',
  [EmploymentApplicationStatus.InterviewCompleted]: 'info',
  [EmploymentApplicationStatus.Offered]: 'ok',
  [EmploymentApplicationStatus.Rejected]: 'bad',
  [EmploymentApplicationStatus.Withdrawn]: 'muted',
};

const JOB_TYPE_LABELS: Record<number, string> = {
  [EmploymentJobType.FullTime]: '全职',
  [EmploymentJobType.Internship]: '实习',
  [EmploymentJobType.PartTime]: '兼职',
  [EmploymentJobType.Apprenticeship]: '学徒',
};

const OUTCOME_STATUS_LABELS: Record<number, string> = {
  [EmploymentOutcomeStatus.Intention]: '就业意向',
  [EmploymentOutcomeStatus.Signed]: '已签约',
  [EmploymentOutcomeStatus.Employed]: '已就业',
  [EmploymentOutcomeStatus.FurtherStudy]: '继续升学',
  [EmploymentOutcomeStatus.Entrepreneurship]: '自主创业',
  [EmploymentOutcomeStatus.Unemployed]: '待就业',
};

const OUTCOME_STATUS_TONES: Record<number, EmploymentTone> = {
  [EmploymentOutcomeStatus.Intention]: 'info',
  [EmploymentOutcomeStatus.Signed]: 'ok',
  [EmploymentOutcomeStatus.Employed]: 'ok',
  [EmploymentOutcomeStatus.FurtherStudy]: 'info',
  [EmploymentOutcomeStatus.Entrepreneurship]: 'warn',
  [EmploymentOutcomeStatus.Unemployed]: 'bad',
};

/** 投递状态文案 */
export function applicationStatusLabel(status?: EmploymentApplicationStatus | null): string {
  if (status === null || status === undefined) return '已投递';
  return APPLICATION_STATUS_LABELS[status] ?? '未知状态';
}

/** 投递状态色调 */
export function applicationStatusTone(status?: EmploymentApplicationStatus | null): EmploymentTone {
  if (status === null || status === undefined) return 'info';
  return APPLICATION_STATUS_TONES[status] ?? 'muted';
}

/** 岗位类型文案 */
export function jobTypeLabel(type?: EmploymentJobType | null): string {
  if (type === null || type === undefined) return '岗位';
  return JOB_TYPE_LABELS[type] ?? '其他类型';
}

/** 就业去向状态文案 */
export function outcomeStatusLabel(status?: EmploymentOutcomeStatus | null): string {
  if (status === null || status === undefined) return '未知状态';
  return OUTCOME_STATUS_LABELS[status] ?? '未知状态';
}

/** 就业去向状态色调 */
export function outcomeStatusTone(status?: EmploymentOutcomeStatus | null): EmploymentTone {
  if (status === null || status === undefined) return 'muted';
  return OUTCOME_STATUS_TONES[status] ?? 'muted';
}

/** 指导记录来源文案（教师指导 / AI 生成） */
export function guidanceSourceLabel(source?: EmploymentGuidanceSourceType | null): string {
  if (source === null || source === undefined) return '就业指导';
  return source === EmploymentGuidanceSourceType.AI ? 'AI 职业规划' : '教师指导';
}

/**
 * 拆分多值文本（技能标签、福利待遇等）。
 * 后端这些字段是逗号/顿号/分号/换行分隔的长文本。
 */
export function splitTagList(value?: string | null): string[] {
  if (!value) return [];
  return value
    .split(/[,，、;；\n]/)
    .map(v => v.trim())
    .filter(Boolean);
}

/**
 * 求职进度阶段：0=未开始 1=已投递 2=面试安排 3=面试完成 4=录用。
 * 用于详情页与投递列表的进度条。
 */
export function applicationStage(status?: EmploymentApplicationStatus | null): number {
  switch (status) {
    case EmploymentApplicationStatus.Submitted:
    case EmploymentApplicationStatus.Viewed:
      return 1;
    case EmploymentApplicationStatus.InterviewScheduled:
      return 2;
    case EmploymentApplicationStatus.InterviewCompleted:
      return 3;
    case EmploymentApplicationStatus.Offered:
      return 4;
    case EmploymentApplicationStatus.Rejected:
      return -1;
    default:
      return 0;
  }
}
