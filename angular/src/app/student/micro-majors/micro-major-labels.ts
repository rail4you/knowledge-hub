import { MicroMajorCertificateStatus } from '../../proxy/micro-majors/enums/micro-major-certificate-status.enum';
import { MicroMajorEnrollmentStatus } from '../../proxy/micro-majors/enums/micro-major-enrollment-status.enum';

/**
 * 学生端 · 微专业域的枚举文案与状态语义（集中一处，禁止散落到组件里）。
 *
 * 核心约定：**报名 ≠ 结业 ≠ 发证** 是三个互不等价的状态。
 * 后端 MicroMajorEnrollment.Status 有 6 个取值：
 *   Pending(5)  待审核          —— 已提交报名，等教师审核
 *   Enrolled(0) 已通过待学习     —— 审核通过，尚未开始学习
 *   InProgress(1) 学习中         —— 课程学习进行中
 *   Completed(2) 已结业          —— 教师核验学习与考核材料后确认结业
 *   Certified(3) 已发证          —— 结业后由教师发放证书
 *   Cancelled(4) 已取消          —— 报名被驳回或撤销
 *
 * 页面必须把「报名 / 结业 / 发证」分开呈现，不得合并成一个「已完成」。
 */

/** 状态色调（与 SCSS 的 .mm-badge--* 对应） */
export type MicroMajorStatusTone = 'pending' | 'active' | 'done' | 'certified' | 'cancelled';

/** 4 步阶梯上的一步 */
export interface MicroMajorStageStep {
  /** 步骤文案 */
  label: string;
  /** 落在该步骤上的后端状态 */
  statuses: MicroMajorEnrollmentStatus[];
}

/**
 * 学习与证书的 4 步阶梯（原型「待审核 → 学习中 → 已结业 → 已发证」）。
 * 注意：步骤用于**展示当前进度**，精确状态文案仍由 MICRO_MAJOR_ENROLLMENT_LABELS 给出。
 */
export const MICRO_MAJOR_STAGE_LADDER: MicroMajorStageStep[] = [
  { label: '待审核', statuses: [MicroMajorEnrollmentStatus.Pending] },
  {
    label: '学习中',
    statuses: [MicroMajorEnrollmentStatus.Enrolled, MicroMajorEnrollmentStatus.InProgress],
  },
  { label: '已结业', statuses: [MicroMajorEnrollmentStatus.Completed] },
  { label: '已发证', statuses: [MicroMajorEnrollmentStatus.Certified] },
];

/** 单个状态的展示信息 */
export interface MicroMajorStatusView {
  /** 精确状态文案 */
  label: string;
  /** 色调 */
  tone: MicroMajorStatusTone;
  /** 阶梯序号（0 起）；不在阶梯上（已取消）时为 -1 */
  stageIndex: number;
}

/** 精确状态文案表：报名 / 学习 / 结业 / 发证 / 取消各自独立 */
export const MICRO_MAJOR_ENROLLMENT_LABELS: Record<MicroMajorEnrollmentStatus, MicroMajorStatusView> = {
  [MicroMajorEnrollmentStatus.Pending]: { label: '待审核', tone: 'pending', stageIndex: 0 },
  [MicroMajorEnrollmentStatus.Enrolled]: { label: '已通过待学习', tone: 'active', stageIndex: 1 },
  [MicroMajorEnrollmentStatus.InProgress]: { label: '学习中', tone: 'active', stageIndex: 1 },
  [MicroMajorEnrollmentStatus.Completed]: { label: '已结业', tone: 'done', stageIndex: 2 },
  [MicroMajorEnrollmentStatus.Certified]: { label: '已发证', tone: 'certified', stageIndex: 3 },
  [MicroMajorEnrollmentStatus.Cancelled]: { label: '已取消', tone: 'cancelled', stageIndex: -1 },
};

/** 证书状态文案 */
export const MICRO_MAJOR_CERTIFICATE_LABELS: Record<MicroMajorCertificateStatus, string> = {
  [MicroMajorCertificateStatus.Active]: '有效',
  [MicroMajorCertificateStatus.Revoked]: '已撤销',
};

/**
 * 「报名 / 结业 / 发证」三态标记。
 *
 * 这三个布尔值互不蕴含：completed 为真不代表 certified 为真，
 * certified 为真一定意味着已完成报名并已结业（但页面仍分别展示）。
 */
export interface MicroMajorStateFlags {
  /** 已提交报名且仍在流程中（待审核 / 已通过 / 学习中） */
  applied: boolean;
  /** 教师已确认结业 */
  completed: boolean;
  /** 已发放证书 */
  certified: boolean;
  /** 报名已取消 */
  cancelled: boolean;
}

/** 由后端状态推导三态标记 */
export function microMajorStateFlags(status?: MicroMajorEnrollmentStatus | null): MicroMajorStateFlags {
  switch (status) {
    case MicroMajorEnrollmentStatus.Pending:
      return { applied: true, completed: false, certified: false, cancelled: false };
    case MicroMajorEnrollmentStatus.Enrolled:
    case MicroMajorEnrollmentStatus.InProgress:
      return { applied: true, completed: false, certified: false, cancelled: false };
    case MicroMajorEnrollmentStatus.Completed:
      return { applied: true, completed: true, certified: false, cancelled: false };
    case MicroMajorEnrollmentStatus.Certified:
      return { applied: true, completed: true, certified: true, cancelled: false };
    case MicroMajorEnrollmentStatus.Cancelled:
      return { applied: false, completed: false, certified: false, cancelled: true };
    default:
      return { applied: false, completed: false, certified: false, cancelled: false };
  }
}

/** 阶梯序号（-1 = 不在阶梯上） */
export function microMajorStageIndex(status?: MicroMajorEnrollmentStatus | null): number {
  if (status === undefined || status === null) return -1;
  return MICRO_MAJOR_ENROLLMENT_LABELS[status]?.stageIndex ?? -1;
}

/** 精确状态文案 */
export function microMajorStatusLabel(status?: MicroMajorEnrollmentStatus | null): string {
  if (status === undefined || status === null) return '未报名';
  return MICRO_MAJOR_ENROLLMENT_LABELS[status]?.label ?? '未知状态';
}
