import { CourseStatus } from '../proxy/courses/enums/course-status.enum';

/**
 * 课程状态文案 / 配色。
 * 与后端 `KnowledgeHub.Courses.Enums.CourseStatus` 一一对应，
 * 管理端课程列表、学生端课程详情共用，避免各处硬编码枚举值导致口径不一致。
 */
export const COURSE_STATUS_LABELS: Record<number, string> = {
  [CourseStatus.Draft]: '草稿',
  [CourseStatus.PendingReview]: '待审核',
  [CourseStatus.SchoolApproved]: '院校已通过',
  [CourseStatus.LeagueApproved]: '联盟已通过',
  [CourseStatus.Rejected]: '已驳回',
  [CourseStatus.Published]: '已发布',
};

export const COURSE_STATUS_COLORS: Record<number, string> = {
  [CourseStatus.Draft]: 'default',
  [CourseStatus.PendingReview]: 'processing',
  [CourseStatus.SchoolApproved]: 'cyan',
  [CourseStatus.LeagueApproved]: 'geekblue',
  [CourseStatus.Rejected]: 'error',
  [CourseStatus.Published]: 'success',
};

/** 状态文案（未知值兜底为「未知」） */
export function courseStatusLabel(status?: number | null): string {
  return COURSE_STATUS_LABELS[status ?? CourseStatus.Draft] ?? '未知';
}

/** 状态标签配色（nz-tag 的 nzColor） */
export function courseStatusColor(status?: number | null): string {
  return COURSE_STATUS_COLORS[status ?? CourseStatus.Draft] ?? 'default';
}

/** 是否已发布：只有已发布课程才会出现在学生端列表中，且允许学生选课 */
export function isCoursePublished(status?: number | null): boolean {
  return status === CourseStatus.Published;
}
