import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { CourseService } from '../../proxy/courses/course.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import { MasteryRadarComponent, type RadarAxis } from '../../shared/charts/mastery-radar.component';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { LearningDashboardDto, LearningProgressDto, StudentCourseDto, RecentLearningDto } from '../../proxy/learning/dtos/models';

interface StatItem {
  label: string;
  value: number;
  suffix: string;
  icon: string;
  color: string;
  hint?: string;
}

interface ExerciseRecordItem {
  id: string;
  courseName: string;
  exerciseTitle: string;
  isCorrect: boolean | null | undefined;
  completedAt: string | null | undefined;
  timeSpent: string | undefined;
  selfAssessment?: number;
}

interface DailyPoint {
  label: string;
  minutes: number;
}

@Component({
  selector: 'app-student-my-learning',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzButtonModule,
    NzSpinModule,
    NzProgressModule,
    NzEmptyModule,
    NzDividerModule,
    MasteryRadarComponent,
  ],
  templateUrl: './student-my-learning.component.html',
  styleUrls: ['./student-my-learning.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyLearningComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly learningService = inject(LearningService);
  private readonly recordService = inject(StudentExerciseRecordService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly dashboard = signal<LearningDashboardDto | null>(null);
  readonly myCourses = signal<StudentCourseDto[]>([]);
  readonly records = signal<ExerciseRecordItem[]>([]);
  readonly recordsLoading = signal(false);

  /** 学习曲线（用最近 7 天） */
  readonly learningCurve = signal<DailyPoint[]>([]);

  readonly stats = signal<StatItem[]>([
    { label: '总课程数', value: 0, suffix: '门', icon: 'book', color: '#1e6ce8' },
    { label: '已完成', value: 0, suffix: '门', icon: 'check-circle', color: '#10b981' },
    { label: '总学时', value: 0, suffix: 'h', icon: 'clock-circle', color: '#06b6d4' },
    { label: '平均进度', value: 0, suffix: '%', icon: 'rise', color: '#0c4cb8' },
  ]);

  readonly inProgressCourses = computed<StudentCourseDto[]>(() =>
    this.myCourses().filter(c => c.status === 1 || c.status === 2 || ((c.progress || 0) > 0 && (c.progress || 0) < 100))
  );

  readonly completedCourses = computed<StudentCourseDto[]>(() =>
    this.myCourses().filter(c => c.status === 3 || (c.progress || 0) >= 100)
  );

  readonly notStartedCourses = computed<StudentCourseDto[]>(() =>
    this.myCourses().filter(c => (c.progress || 0) === 0 && c.status !== 3)
  );

  readonly recentLearnings = computed<RecentLearningDto[]>(() => {
    return this.dashboard()?.recentLearning || [];
  });

  /** 掌握度（基于 knowledgeDimensions + masteryValues） */
  readonly knowledgeStats = computed(() => {
    const dash = this.dashboard();
    if (!dash?.knowledgeDimensions?.length) return [];
    return dash.knowledgeDimensions.slice(0, 6).map((d, i) => ({
      name: d.name || '维度' + (i + 1),
      max: d.maxValue || 100,
      value: dash.masteryValues?.[i] ?? 0,
    }));
  });

  /** 掌握度雷达图数据 */
  readonly radarData = computed<RadarAxis[]>(() =>
    this.knowledgeStats().map(s => ({
      name: s.name,
      value: s.value,
      max: s.max,
    }))
  );

  /** 掌握度平均值 */
  readonly radarAverage = computed<number>(() => {
    const stats = this.knowledgeStats();
    if (stats.length === 0) return 0;
    return Math.round(stats.reduce((s, k) => s + k.value, 0) / stats.length);
  });

  ngOnInit() {
    this.loadAll();
  }

  loadAll() {
    this.loading.set(true);
    this.learningService.getDashboard().subscribe({
      next: data => {
        this.dashboard.set(data);
        this.updateStats(data);
        this.buildLearningCurve(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('学习数据加载失败');
      },
    });

    this.learningService.getMyCourses().subscribe({
      next: list => this.myCourses.set(list || []),
      error: () => this.myCourses.set([]),
    });

    this.loadRecords();
  }

  loadRecords() {
    this.recordsLoading.set(true);
    // 拉取最近 10 条跨课程记录
    this.recordService.getMyRecentRecords({
      skipCount: 0,
      maxResultCount: 10,
    } as any).subscribe({
      next: result => {
        const items = (result?.items || []).map((r: any) => ({
          id: r.id,
          courseName: r.courseName || '未命名课程',
          exerciseTitle: r.exerciseTitle || '习题练习',
          isCorrect: r.isCorrect,
          completedAt: r.completedAt,
          timeSpent: r.timeSpent,
          selfAssessment: r.selfAssessment,
        }));
        this.records.set(items);
        this.recordsLoading.set(false);
      },
      error: () => {
        this.records.set([]);
        this.recordsLoading.set(false);
      },
    });
  }

  private updateStats(dash: LearningDashboardDto) {
    const totalMinutes = dash.totalLearningTime || 0;
    const totalHours = Math.round((totalMinutes / 60) * 10) / 10;
    this.stats.set([
      { label: '总课程数', value: dash.totalCourses || 0, suffix: '门', icon: 'book', color: '#1e6ce8' },
      { label: '已完成', value: dash.completedCourses || 0, suffix: '门', icon: 'check-circle', color: '#10b981' },
      { label: '总学时', value: totalHours, suffix: 'h', icon: 'clock-circle', color: '#06b6d4' },
      { label: '平均进度', value: Math.round(dash.averageProgress || 0), suffix: '%', icon: 'rise', color: '#0c4cb8' },
    ]);
  }

  private buildLearningCurve(dash: LearningDashboardDto) {
    const labels = dash.dailyTimeLabels || [];
    const values = dash.dailyTimeValues || [];
    if (labels.length === 0) {
      // 演示数据
      this.learningCurve.set([
        { label: '周一', minutes: 35 },
        { label: '周二', minutes: 52 },
        { label: '周三', minutes: 28 },
        { label: '周四', minutes: 64 },
        { label: '周五', minutes: 45 },
        { label: '周六', minutes: 80 },
        { label: '周日', minutes: 72 },
      ]);
      return;
    }
    this.learningCurve.set(labels.map((l, i) => ({
      label: l,
      minutes: values[i] || 0,
    })));
  }

  openCourse(id: string) {
    this.router.navigate(['/student/courses', id]);
  }

  goCourses() {
    this.router.navigate(['/student/courses']);
  }

  /** 课程封面渐变 */
  courseGradient(course: StudentCourseDto | { courseTitle?: string; courseId?: string; major?: string }): string {
    return this.gradientByKey(
      course?.courseTitle || course?.courseId || 'x',
      course?.major || ''
    );
  }

  private gradientByKey(primary: string, secondary: string): string {
    const palettes = [
      '#0c4cb8',
      '#1e6ce8',
      '#1e6ce8',
      '#0891b2',
      '#0284c7',
      '#059669',
      '#10b981',
      '#0e7490',
    ];
    const key = (primary || 'x') + (secondary || '');
    let hash = 0;
    for (let i = 0; i < key.length; i++) {
      hash = (hash * 31 + key.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
  }

  hasCover(c: StudentCourseDto): boolean {
    return !!c.courseCoverImageUrl && c.courseCoverImageUrl.trim().length > 0;
  }

  statusLabel(s?: number): string {
    const map: Record<number, string> = {
      0: '未开始',
      1: '学习中',
      2: '学习中',
      3: '已完成',
    };
    return map[s || 0] || '学习中';
  }

  statusColor(s?: number): string {
    const map: Record<number, string> = {
      0: '#94a3b8',
      1: '#1e6ce8',
      2: '#06b6d4',
      3: '#10b981',
    };
    return map[s || 0] || '#1e6ce8';
  }

  /** 找到学习曲线最大值 */
  get curveMax(): number {
    return Math.max(1, ...this.learningCurve().map(p => p.minutes));
  }

  /** 计算柱状图高度百分比 */
  barHeight(minutes: number): number {
    return Math.max(4, (minutes / this.curveMax) * 100);
  }

  selfAssessmentLabel(v?: number): string {
    const map: Record<number, string> = {
      0: '困难',
      1: '一般',
      2: '良好',
      3: '优秀',
    };
    return map[v || 0] || '未评估';
  }
}
