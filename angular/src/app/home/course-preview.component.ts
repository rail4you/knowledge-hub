import { ChangeDetectionStrategy, Component } from '@angular/core';
import { StudentCourseDetailComponent } from '../student/course-detail/student-course-detail.component';

/**
 * 公开的课程预览页。
 *
 * 需求背景：门户首页的精选课程卡片以前直接跳 /student/courses/:id，
 * 而整棵 /student 路由挂着登录守卫，游客点进去只能看到登录页 —— 课程简介、
 * 大纲这类公开信息白白浪费了。
 *
 * 这里刻意不复用 /student 下的页面，而是包一层 previewMode 的只读视图：
 * - 排版与学生端详情页完全一致（同一个组件，避免两份模板走样）
 * - 隐藏“相关课程”Tab，只讲当前这一门课
 * - 已选课的主按钮跳学生端学习页，未选课保持禁用
 * - 返回按钮固定回首页课程区块
 */
@Component({
  selector: 'app-course-preview',
  standalone: true,
  imports: [StudentCourseDetailComponent],
  template: '<app-student-course-detail [previewMode]="true" />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CoursePreviewComponent {}
