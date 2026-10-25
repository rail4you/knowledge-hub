import { ClassroomAgentAssignmentStatus } from '../../proxy/teaching-agents/enums/classroom-agent-assignment-status.enum';
import { ClassroomAgentTaskPublishStatus } from '../../proxy/teaching-agents/enums/classroom-agent-task-publish-status.enum';
import { ClassroomAgentTaskTargetType } from '../../proxy/teaching-agents/enums/classroom-agent-task-target-type.enum';

/**
 * 学生端 · 课堂任务枚举标签映射。
 *
 * 规范要求：枚举标签集中放在模块自己的 *-labels.ts 里，
 * 不在组件模板里散落 switch。
 */

/** 任务分配状态 → 中文标签 */
export const ASSIGNMENT_STATUS_LABEL: Record<number, string> = {
  [ClassroomAgentAssignmentStatus.Pending]: '待开始',
  [ClassroomAgentAssignmentStatus.InProgress]: '进行中',
  [ClassroomAgentAssignmentStatus.Submitted]: '已提交',
  [ClassroomAgentAssignmentStatus.NeedsTeacherHelp]: '待教师回复',
};

/** 任务发布状态 → 中文标签 */
export const PUBLISH_STATUS_LABEL: Record<number, string> = {
  [ClassroomAgentTaskPublishStatus.Draft]: '草稿',
  [ClassroomAgentTaskPublishStatus.Published]: '已发布',
  [ClassroomAgentTaskPublishStatus.Closed]: '已结束',
};

/** 任务关联对象类型 → 中文标签 */
export const TARGET_TYPE_LABEL: Record<number, string> = {
  [ClassroomAgentTaskTargetType.Course]: '课程任务',
  [ClassroomAgentTaskTargetType.Resource]: '资源任务',
  [ClassroomAgentTaskTargetType.ExerciseSet]: '习题任务',
};

/** 状态标签样式后缀（配合 SCSS 的 .at-status--* ） */
export function assignmentStatusClass(status?: number | null): string {
  switch (status) {
    case ClassroomAgentAssignmentStatus.InProgress:
      return 'progress';
    case ClassroomAgentAssignmentStatus.Submitted:
      return 'done';
    case ClassroomAgentAssignmentStatus.NeedsTeacherHelp:
      return 'help';
    default:
      return 'pending';
  }
}
