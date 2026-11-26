import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzMessageService } from 'ng-zorro-antd/message';
import { LearningProfileService } from '../../proxy/learning/learning-profile.service';
import type {
  CourseLearningProfileDto,
  StudentLearningProfileDto,
  StudentActivitySummaryDto,
} from '../../proxy/learning/dtos/models';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { MyCoursesService } from '../shared/my-courses.service';
import { CourseContextService } from '../course-context.service';

/**
 * 学习画像（教学数据画像 · 页 1）。
 *
 * 课程维度：KPI + 近 30 天活跃趋势 + 行为分布 + 活跃/未活跃名单；
 * 点学生 → 抽屉看个人画像（偏好学习方式 / 活跃时段 / 连续性 / 近期活跃度 / 薄弱知识点 / 行为分布）。
 *
 * 口径说明（与学生端一致）：数据只来自行为埋点 + 掌握度，不手填标签。
 */
@Component({
  selector: 'app-learning-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTableModule,
    NzTagModule,
    NzProgressModule,
    NzDrawerModule,
  ],
  templateUrl: './learning-profile.component.html',
  styleUrls: ['./learning-profile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LearningProfileComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(LearningProfileService);
  private readonly message = inject(NzMessageService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly days = signal(30);

  readonly profile = signal<CourseLearningProfileDto | null>(null);
  readonly loading = signal(false);

  readonly studentProfile = signal<StudentLearningProfileDto | null>(null);
  readonly studentLoading = signal(false);
  readonly studentOpen = signal(false);

  /** 趋势图：按最大值归一化成柱高（纯 CSS 条形，避免引图表库） */
  readonly trendBars = computed(() => {
    const points = this.profile()?.timeline ?? [];
    const max = Math.max(1, ...points.map(p => p.count));
    return points.map(p => ({
      date: p.date,
      count: p.count,
      height: Math.round((p.count / max) * 100),
    }));
  });

  readonly actionBars = computed(() => {
    const rows = this.profile()?.actionDistribution ?? [];
    const max = Math.max(1, ...rows.map(r => r.count));
    return rows.map(r => ({ label: r.label, count: r.count, width: Math.round((r.count / max) * 100) }));
  });

  ngOnInit(): void {
    this.loadCourses();
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      const courseId = params.get('courseId') || this.courseContext.currentCourseId();
      if (courseId) this.onCourseSelected(courseId);
    });
  }

  loadCourses(): void {
    this.myCourses.getCourses(300).subscribe({
      next: list => this.courses.set(list as any),
      error: () => this.courses.set([]),
    });
  }

  onCourseSelected(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    if (!courseId) {
      this.profile.set(null);
      return;
    }
    this.courseContext.setCourse(courseId);
    this.load();
  }

  onDaysChange(days: number): void {
    this.days.set(days);
    this.load();
  }

  load(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.service.getCourseProfile(courseId, this.days()).subscribe({
      next: data => {
        this.profile.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.profile.set(null);
        this.loading.set(false);
        this.message.error('画像加载失败');
      },
    });
  }

  openStudent(row: StudentActivitySummaryDto): void {
    const courseId = this.selectedCourseId();
    if (!courseId || !row.studentId) return;

    this.studentProfile.set(null);
    this.studentOpen.set(true);
    this.studentLoading.set(true);
    this.service.getStudentProfile(row.studentId, courseId).subscribe({
      next: data => {
        this.studentProfile.set(data);
        this.studentLoading.set(false);
      },
      error: () => {
        this.studentLoading.set(false);
        this.message.error('学生画像加载失败');
      },
    });
  }

  percent(value?: number | null): number {
    return Math.round((value ?? 0) * 100);
  }
}
