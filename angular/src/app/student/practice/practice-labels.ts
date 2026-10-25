import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';

/**
 * 「专项练习」枚举标签与常量。
 *
 * 规范要求：枚举标签集中放在模块自己的 `*-labels.ts`，不要散落在模板里。
 */

/** 题型 → 中文标签 */
export const EXERCISE_TYPE_LABELS: Record<ExerciseType, string> = {
  [ExerciseType.SingleChoice]: '单选题',
  [ExerciseType.MultiChoice]: '多选题',
  [ExerciseType.TrueFalse]: '判断题',
  [ExerciseType.FillBlank]: '填空题',
  [ExerciseType.ShortAnswer]: '简答题',
  [ExerciseType.Essay]: '论述题',
  [ExerciseType.CaseAnalysis]: '案例分析题',
  [ExerciseType.TermExplanation]: '名词解释题',
  [ExerciseType.Calculation]: '计算题',
  [ExerciseType.Programming]: '编程题',
};

/** 一次练习取题数量（后端接口的 take 参数） */
export const PRACTICE_TAKE_SIZE = 6;

/** 作答方式 */
export const ANSWER_MODE = {
  choice: 'choice',
  multi: 'multi',
  judge: 'judge',
  text: 'text',
} as const;

export type AnswerMode = (typeof ANSWER_MODE)[keyof typeof ANSWER_MODE];

/** 判分反馈文案 */
export const FEEDBACK_TEXT = {
  correct: '回答正确',
  wrong: '回答错误',
  pending: '已提交，主观题需老师批改',
} as const;

/** 空态文案 */
export const EMPTY_TEXT = {
  noCourse: '还没有指定练习目标。请从「智学分析」选择薄弱知识点后进入专项练习，或先去课程中心完成一次练习。',
  noItems: '暂无可练习的题目。先完成课程练习或等待老师确认试卷后，系统会按薄弱知识点推荐题目。',
} as const;
