import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Router } from '@angular/router';
import { LearningService } from '../../proxy/learning/learning.service';
import { StudentCourseDto } from '../../proxy/learning/dtos/models';

@Component({
  selector: 'app-learning-progress',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    NzButtonModule,
    NzProgressModule,
    NzIconModule,
    NzSpinModule,
    NzTagModule,
    NzEmptyModule,
    NzCollapseModule
  ],
  templateUrl: './learning-progress.component.html',
  styleUrls: ['./learning-progress.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LearningProgressComponent implements OnInit {
  private readonly learningService = inject(LearningService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  loading = signal(true);
  courses = signal<StudentCourseDto[]>([]);

  ngOnInit() {
    this.loadCourses();
  }

  loadCourses() {
    this.loading.set(true);
    this.learningService.getMyCourses().subscribe({
      next: (courses) => {
        this.courses.set(courses);
        this.loading.set(false);
      },
      error: () => {
        this.message.error('加载课程列表失败');
        this.loading.set(false);
      }
    });
  }

  getProgressColor(progress: number): string {
    if (progress >= 80) return '#52c41a';
    if (progress >= 50) return '#faad14';
    return '#ff4d4f';
  }

  getStatusLabel(status: number): string {
    const labels: Record<number, string> = { 0: '已选课', 1: '学习中', 2: '已完成', 3: '已退课' };
    return labels[status] ?? '未知';
  }

  getStatusColor(status: number): string {
    const colors: Record<number, string> = { 0: 'default', 1: 'processing', 2: 'success', 3: 'error' };
    return colors[status] ?? 'default';
  }

  startExercise(courseId: string) {
    this.router.navigate(['/learning/exercise-learning', courseId]);
  }

  viewCourseDetail(courseId: string) {
    this.router.navigate(['/learning/course-detail', courseId]);
  }
}
