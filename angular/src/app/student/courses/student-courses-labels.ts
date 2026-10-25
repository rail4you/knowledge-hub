/**
 * 学生端 · 课程中心 —— 枚举标签与筛选项。
 *
 * 规范要求：枚举映射表集中放在模块级 `*-labels.ts`，不要散落在组件里。
 * 这里只放「后端枚举值 → 中文标签」的静态映射与筛选项定义，不含任何假数据。
 */

/** 课程筛选范围（两个真实的服务端分页数据源） */
export type CourseScope = 'published' | 'shared';

export interface CourseScopeOption {
  value: CourseScope;
  label: string;
  hint: string;
}

/**
 * 筛选范围：
 *   published —— GET /api/app/course/published（本校已发布课程）
 *   shared    —— GET /api/app/course-share/shared-to-me（共享到本校的课程）
 */
export const COURSE_SCOPE_OPTIONS: CourseScopeOption[] = [
  {
    value: 'published',
    label: '本校已发布课程',
    hint: '本机构教师建设并已发布的课程',
  },
  {
    value: 'shared',
    label: '共享到本校的课程',
    hint: '其它学校共享到本机构的课程',
  },
];

/** 课程难度（后端 CourseDto.difficulty：1..5） */
export const COURSE_DIFFICULTY_LABELS = ['入门', '初级', '中级', '高级', '专家'];

/** 判断 URL 上的 scope 参数是否合法 */
export function isCourseScope(value: string | null | undefined): value is CourseScope {
  return value === 'published' || value === 'shared';
}
