import { SelfAssessment } from '../../proxy/learning/enums/self-assessment.enum';
import { StudentCourseStatus } from '../../proxy/learning/enums/student-course-status.enum';

/**
 * 「我的学习」枚举标签表。
 *
 * 规范要求：枚举标签集中放在模块自己的 `*-labels.ts` 里，不要散落在模板中。
 * 这里全部由后端枚举值驱动，避免手写魔法数字导致枚举升级后标签错位。
 */

/** 选课状态 → 中文标签 */
export const COURSE_STATUS_LABELS: Record<StudentCourseStatus, string> = {
  [StudentCourseStatus.Enrolled]: '未开始',
  [StudentCourseStatus.InProgress]: '学习中',
  [StudentCourseStatus.Completed]: '已完成',
  [StudentCourseStatus.Dropped]: '已退课',
};

/** 习题自评 → 中文标签 */
export const SELF_ASSESSMENT_LABELS: Record<SelfAssessment, string> = {
  [SelfAssessment.None]: '未评估',
  [SelfAssessment.Correct]: '完全掌握',
  [SelfAssessment.PartiallyCorrect]: '基本掌握',
  [SelfAssessment.Incorrect]: '仍不熟悉',
};

/** 习题记录判定结果 → 中文标签 */
export const RECORD_RESULT_LABELS: {
  correct: string;
  wrong: string;
  unknown: string;
} = {
  correct: '答对',
  wrong: '答错',
  unknown: '未判定',
};

/** 习题记录筛选（写入 URL 的 query 参数） */
export type RecordFilter = 'all' | 'correct' | 'wrong';

/** 筛选 → 后端 isCorrect 取值；all 表示不传该条件 */
export const RECORD_FILTER_TO_IS_CORRECT: Record<RecordFilter, number | undefined> = {
  all: undefined,
  correct: 1,
  wrong: 0,
};

/** 筛选 → 中文标签 */
export const RECORD_FILTER_LABELS: Record<RecordFilter, string> = {
  all: '全部记录',
  correct: '答对',
  wrong: '答错',
};
