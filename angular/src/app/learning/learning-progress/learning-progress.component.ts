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

  // Expandable learning records (per student)
  expandedStudentId = signal<string | null>(null);
  expandedRecordsMap = signal<Record<string, StudentExerciseRecordDto[]>>({});
  loadingRecordsMap = signal<Record<string, boolean>>({});

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
    this.expandedStudentId.set(null);
    this.expandedRecordsMap.set({});
    this.loadingRecordsMap.set({});
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

  hasRecords(stat: StudentLearningStatisticsDto): boolean {
    return (stat.completedCount ?? 0) > 0;
  }

  isExpanded(studentId: string): boolean {
    return this.expandedStudentId() === studentId;
  }

  getExpandedRecords(studentId: string): StudentExerciseRecordDto[] {
    return this.expandedRecordsMap()[studentId] ?? [];
  }

  isLoadingRecords(studentId: string): boolean {
    return !!this.loadingRecordsMap()[studentId];
  }

  toggleRecords(stat: StudentLearningStatisticsDto) {
    if (!stat.studentId) return;
    if (!this.hasRecords(stat)) return;
    const id = stat.studentId;
    if (this.expandedStudentId() === id) {
      this.expandedStudentId.set(null);
      return;
    }
    this.expandedStudentId.set(id);
    // keep legacy studentRecords in sync for any external usage
    const cached = this.expandedRecordsMap()[id];
    if (cached) {
      this.studentRecords.set(cached);
      return;
    }
    this.loadRecordsForStudent(id);
  }

  onExpandChange(stat: StudentLearningStatisticsDto, expanded: boolean) {
    if (!stat.studentId) return;
    if (!this.hasRecords(stat)) return;
    if (expanded) {
      this.expandedStudentId.set(stat.studentId);
      const cached = this.expandedRecordsMap()[stat.studentId];
      if (!cached) this.loadRecordsForStudent(stat.studentId);
      else this.studentRecords.set(cached);
    } else {
      if (this.expandedStudentId() === stat.studentId) this.expandedStudentId.set(null);
    }
  }

  private loadRecordsForStudent(studentId: string) {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.loadingRecordsMap.update(m => ({ ...m, [studentId]: true }));
    this.statsService.getStudentRecords({
      courseId,
      skipCount: 0,
      maxResultCount: 50,
    }, studentId).subscribe({
      next: (result) => {
        const items = result.items || [];
        this.expandedRecordsMap.update(m => ({ ...m, [studentId]: items }));
        this.studentRecords.set(items);
        this.loadingRecordsMap.update(m => ({ ...m, [studentId]: false }));
      },
      error: () => {
        this.loadingRecordsMap.update(m => ({ ...m, [studentId]: false }));
        this.message.error('加载学习记录失败');
      },
    });
  }

  viewStudentRecords(studentId: string) {
    const stat = this.studentStats().find(s => s.studentId === studentId);
    if (stat) {
      this.toggleRecords(stat);
      return;
    }
    // fallback: direct id (legacy calls)
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    // treat as expand toggle if we can find or just load
    if (this.expandedStudentId() === studentId) {
      this.expandedStudentId.set(null);
      return;
    }
    this.expandedStudentId.set(studentId);
    if (this.expandedRecordsMap()[studentId]) {
      this.studentRecords.set(this.expandedRecordsMap()[studentId]);
      return;
    }
    this.loadRecordsForStudent(studentId);
  }

  formatTimeSpan(ts?: string): string {
    if (!ts) return '0分钟';
    // TimeSpan from .NET: "hh:mm:ss" or "d.hh:mm:ss" / "hh:mm:ss.fffffff"
    // Parse to total seconds for accurate display, keep consistent with top totalLearningMinutes (which sums LearningProgress + exercise time)
    const normalized = ts.split('.')[0];
    const parts = normalized.split(':');
    let totalSeconds = 0;
    if (parts.length === 3) {
      const h = parseInt(parts[0], 10) || 0;
      const m = parseInt(parts[1], 10) || 0;
      const s = parseInt(parts[2], 10) || 0;
      totalSeconds = h * 3600 + m * 60 + s;
    } else if (parts.length === 2) {
      const m = parseInt(parts[0], 10) || 0;
      const s = parseInt(parts[1], 10) || 0;
      totalSeconds = m * 60 + s;
    } else {
      return ts;
    }
    if (totalSeconds === 0) return '0分钟';
    if (totalSeconds < 60) return `${totalSeconds}秒`;
    const h = Math.floor(totalSeconds / 3600);
    const m = Math.floor((totalSeconds % 3600) / 60);
    const s = totalSeconds % 60;
    if (h > 0) {
      if (m > 0) return `${h}小时${m}分钟`;
      return `${h}小时`;
    }
    if (s > 0 && m < 60) return `${m}分${s}秒`;
    return `${m}分钟`;
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

  /** Tag preset: high/medium keep colored tag, low uses neutral default to avoid solid red background with poor contrast */
  getRateTagPreset(rate: number): 'success' | 'warning' | 'default' {
    if (rate >= 80) return 'success';
    if (rate >= 60) return 'warning';
    return 'default';
  }
}
