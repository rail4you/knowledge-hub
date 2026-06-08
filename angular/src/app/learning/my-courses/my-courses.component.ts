import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';

@Component({
  selector: 'app-my-courses',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    NzButtonModule,
    NzProgressModule,
    NzTagModule,
    NzSpinModule,
    NzEmptyModule,
    NzGridModule,
    NzIconModule
  ],
  templateUrl: './my-courses.component.html',
  styleUrls: ['./my-courses.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MyCoursesComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly router = inject(Router);
  
  courses = signal<CourseDto[]>([]);
  loading = signal(false);

  ngOnInit() {
    this.loadCourses();
  }
  
  loadCourses() {
    this.loading.set(true);
    this.courseService.getMyCourses({ maxResultCount: 100 } as any).subscribe({
      next: (result) => {
        this.courses.set(result.items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }
  
  // 关键修复 P1-23：原实现只用 switch 处理 1/2/3，且 default 全部回退到"初级"，
  // 与学生端 student-courses.component / student-course-detail.component 的
  // ['入门','初级','中级','高级','专家'] 五档映射不一致。
  // 教师端打开"我的课程"时会看到课程的"入门"档被错渲染为"初级"，用户感知为"0 入门"
  // 文案含义不明。统一为数组下标方式，并对 0/null/undefined 显式返回"未设置"。
  getDifficultyLabel(difficulty?: number): string {
    const labels = ['入门', '初级', '中级', '高级', '专家'];
    if (!difficulty || difficulty < 1 || difficulty > labels.length) return '未设置';
    return labels[difficulty - 1];
  }

  getDifficultyColor(difficulty?: number): string {
    // 颜色按档位从低到高递进，0/越界统一用 blue（"未设置"语义中性色）。
    const colors = ['blue', 'green', 'cyan', 'orange', 'red'];
    if (!difficulty || difficulty < 1 || difficulty > colors.length) return 'blue';
    return colors[difficulty - 1];
  }

  continueLearning(courseId: string) {
    const route = this.router.url.startsWith('/student')
      ? ['/student/course-detail', courseId]
      : ['/learning/course-detail', courseId];
    this.router.navigate(route);
  }
  
  viewDetail(courseId: string) {
    const route = this.router.url.startsWith('/student')
      ? ['/student/course-detail', courseId]
      : ['/learning/course-detail', courseId];
    this.router.navigate(route);
  }

  browseCourses() {
    const route = this.router.url.startsWith('/student')
      ? ['/student/resources']
      : ['/learning/course-list'];
    this.router.navigate(route);
  }
}
