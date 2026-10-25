import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';

/**
 * 学生端 · 课程学习页 —— 枚举标签与样式映射。
 *
 * 规范要求：枚举映射表集中放在模块级 `*-labels.ts`，不要散落在组件里。
 * 这里只有「后端枚举值 → 中文标签 / CSS 类名」的静态映射，不含任何假数据。
 */

/** 难度（后端 KnowledgeResourceDto.difficulty / CourseDto.difficulty：1..5） */
export const DIFFICULTY_LABELS = ['入门', '初级', '中级', '高级', '专家'];

/** 重要程度（后端 KnowledgeResourceDto.importanceLevel） */
export const IMPORTANCE_LABELS: Record<string, string> = {
  core: '核心',
  important: '重要',
  normal: '一般',
  extended: '拓展',
};

/**
 * 重要程度 → CSS 类名。
 *
 * 颜色一律由 SCSS 里的 CSS 变量决定，组件 TS 不再写死色值。
 */
export const IMPORTANCE_CLASSES: Record<string, string> = {
  core: 'is-core',
  important: 'is-important',
  normal: 'is-normal',
  extended: 'is-extended',
};

/** 题型（后端 ExerciseType 枚举，顺序与枚举值一一对应） */
export const EXERCISE_TYPE_LABELS = [
  '单选题',
  '多选题',
  '判断题',
  '填空题',
  '简答题',
  '论述题',
  '案例分析',
  '名词解释',
  '计算题',
  '编程题',
];

/** 难度文案 */
export function difficultyLabel(d?: number | null): string {
  return DIFFICULTY_LABELS[(d || 1) - 1] || '未设置';
}

/** 重要程度文案 */
export function importanceLabel(level?: string | null): string {
  return IMPORTANCE_LABELS[level || 'normal'] || '一般';
}

/** 重要程度样式类（颜色由 SCSS 变量控制） */
export function importanceClass(level?: string | null): string {
  return IMPORTANCE_CLASSES[level || 'normal'] || IMPORTANCE_CLASSES['normal'];
}

/** 题型文案 */
export function exerciseTypeLabel(t?: ExerciseType | number | null): string {
  return EXERCISE_TYPE_LABELS[t || 0] || '未知';
}
