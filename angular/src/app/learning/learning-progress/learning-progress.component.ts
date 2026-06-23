import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTableModule } from 'ng-zorro-antd/table';

import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseService } from '../../proxy/courses/course.service';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { CourseLearningOverviewDto, StudentLearningStatisticsDto, StudentExerciseRecordDto } from '../../proxy/learning/dtos/models';

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
    NzTableModule,
  ],
  templateUrl: './learning-progress.component.html',
  styleUrls: ['./learning-progress.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LearningProgressComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly statsService = inject(StudentExerciseRecordService);
  private readonly message = inject(NzMessageService);

  // Course list
  loadingCourses = signal(false);
  courses = signal<CourseDto[]>([]);
  courseOverviewMap = signal<Record<string, CourseLearningOverviewDto>>({});

  // Selected course detail
  selectedCourseId = signal<string | null>(null);
  selectedCourse = computed(() => {
    const id = this.selectedCourseId();
    if (!id) return null;
    return this.courses().find(c => c.id === id) ?? null;
  });
  selectedOverview = computed(() => {
    const id = this.selectedCourseId();
    if (!id) return null;
    return this.courseOverviewMap()[id] ?? null;
  });

  // Student statistics
  loadingStudents = signal(false);
  studentStats = signal<StudentLearningStatisticsDto[]>([]);
  studentRecords = signal<StudentExerciseRecordDto[]>([]);

  ngOnInit() {
    this.loadCourses();
  }

  loadCourses() {
    this.loadingCourses.set(true);
    this.courseService.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe({
      next: (result) => {
        const courses = result.items || [];
        this.courses.set(courses);
        // Load overview for each course
        courses.forEach(c => {
          if (c.id) this.loadCourseOverview(c.id);
        });
        this.loadingCourses.set(false);
      },
      error: () => {
        this.loadingCourses.set(false);
        this.message.error('加载课程列表失败');
      },
    });
  }

  private loadCourseOverview(courseId: string) {
    this.statsService.getCourseLearningOverview({ courseId }).subscribe({
      next: (overview) => {
        this.courseOverviewMap.update(m => ({ ...m, [courseId]: overview }));
      },
      error: () => {
        // Silent fail for individual overviews
      },
    });
  }

  selectCourse(courseId: string) {
    this.selectedCourseId.set(courseId);
    this.studentStats.set([]);
    this.studentRecords.set([]);
    if (courseId) {
      this.loadStudentStats(courseId);
    }
  }

  private loadStudentStats(courseId: string) {
    this.loadingStudents.set(true);
    this.statsService.getLearningStatistics({
      courseId,
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe({
      next: (result) => {
        this.studentStats.set(result.items || []);
        this.loadingStudents.set(false);
      },
      error: () => {
        this.loadingStudents.set(false);
        this.message.error('加载学习统计失败');
      },
    });
  }

  viewStudentRecords(studentId: string) {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.statsService.getStudentRecords({
      courseId,
      skipCount: 0,
      maxResultCount: 50,
    }, studentId).subscribe({
      next: (result) => {
        this.studentRecords.set(result.items || []);
      },
      error: () => {
        this.message.error('加载学习记录失败');
      },
    });
  }

  formatTimeSpan(ts?: string): string {
    if (!ts) return '0分钟';
    // Format time span string like "hh:mm:ss" to readable form
    const parts = ts.split(':');
    if (parts.length === 3) {
      const h = parseInt(parts[0]);
      const m = parseInt(parts[1]);
      if (h > 0) return `${h}小时${m}分钟`;
      if (m > 0) return `${m}分钟`;
      return '不到1分钟';
    }
    return ts;
  }

  formatDate(d?: string | null): string {
    if (!d) return '-';
    return new Date(d).toLocaleString('zh-CN', {
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  getScoreColor(rate: number): string {
    if (rate >= 80) return '#52c41a';
    if (rate >= 60) return '#faad14';
    return '#ff4d4f';
  }
}
