import { LiveType } from '../../proxy/recruitment-live/live-type.enum';
import { RecruitmentLiveStatus } from '../../proxy/recruitment-live/recruitment-live-status.enum';

/**
 * 学生端 · 直播安排枚举标签映射。
 * 规范要求枚举标签集中在模块自己的 *-labels.ts 中。
 */

/** 直播类型 → 中文 */
export const LIVE_TYPE_LABEL: Record<number, string> = {
  [LiveType.Recruitment]: '招聘直播',
  [LiveType.Course]: '课程直播',
};

/** 直播状态 → 中文 */
export const LIVE_STATUS_LABEL: Record<number, string> = {
  [RecruitmentLiveStatus.Waiting]: '等待中',
  [RecruitmentLiveStatus.Active]: '进行中',
  [RecruitmentLiveStatus.Ended]: '已结束',
  [RecruitmentLiveStatus.Cancelled]: '已取消',
};
