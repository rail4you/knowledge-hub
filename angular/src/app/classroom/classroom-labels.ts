import { CheckInSessionStatus } from '../proxy/classroom/enums/check-in-session-status.enum';
import { ClassroomPracticeStatus } from '../proxy/classroom/enums/classroom-practice-status.enum';
import { GroupTaskStatus } from '../proxy/classroom/enums/group-task-status.enum';
import { GroupMode } from '../proxy/classroom/enums/group-mode.enum';

/**
 * 课堂互动（签到 / 讨论答疑 / 小组任务 / 课堂练习）的枚举文案与常量。
 * 规范要求：枚举 label 集中在模块自己的 `*-labels.ts`，不要散落在模板里。
 */

export const CHECK_IN_STATUS_LABEL: Record<CheckInSessionStatus, string> = {
  [CheckInSessionStatus.Active]: '进行中',
  [CheckInSessionStatus.Closed]: '已结束',
};

export const PRACTICE_STATUS_LABEL: Record<ClassroomPracticeStatus, string> = {
  [ClassroomPracticeStatus.Active]: '进行中',
  [ClassroomPracticeStatus.Closed]: '已结束',
};

export const GROUP_TASK_STATUS_LABEL: Record<GroupTaskStatus, string> = {
  [GroupTaskStatus.Draft]: '草稿',
  [GroupTaskStatus.Published]: '已发布',
  [GroupTaskStatus.Closed]: '已结束',
};

export const GROUP_MODE_LABEL: Record<GroupMode, string> = {
  [GroupMode.TeacherAssigned]: '手工分组',
  [GroupMode.RandomAuto]: '随机分组',
};

/** 签到默认有效期（分钟） */
export const DEFAULT_CHECK_IN_MINUTES = 10;

/** 课堂练习默认题量 */
export const DEFAULT_PRACTICE_QUESTION_COUNT = 5;

/** 小组任务默认每组人数（随机分组时用） */
export const DEFAULT_GROUP_SIZE = 4;

/** 短链前缀（与后端 DTO 的 ShortUrl 保持一致） */
export const SHORT_URL_PREFIX = {
  checkIn: '/check-in/',
  groupTask: '/group-task/',
  practice: '/classroom-practice/',
} as const;

/** 拼出可复制/可投屏的完整短链 */
export function buildShortUrl(prefix: string, token: string): string {
  const origin = typeof window !== 'undefined' ? window.location.origin : '';
  return `${origin}${prefix}${token}`;
}

/** nz-tag 颜色：状态 → 颜色 */
export const STATUS_TAG_COLOR: Record<string, string> = {
  进行中: 'processing',
  已结束: 'default',
  已发布: 'success',
  草稿: 'warning',
  已截止: 'default',
  未发布: 'warning',
};
