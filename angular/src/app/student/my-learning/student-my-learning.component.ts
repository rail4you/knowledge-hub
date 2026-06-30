import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, computed, inject, signal, viewChild, ElementRef } from '@angular/core';
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
import * as echarts from 'echarts/core';
import { LineChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent, GridComponent } from 'echarts/components';
import { CourseService } from '../../proxy/courses/course.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import { MasteryRadarComponent, type RadarAxis } from '../../shared/charts/mastery-radar.component';

echarts.use([LineChart, CanvasRenderer, TooltipComponent, GridComponent]);
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { LearningDashboardDto, LearningProgressDto, StudentCourseListItemDto, RecentLearningDto } from '../../proxy/learning/dtos/models';

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
export class StudentMyLearningComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
  private readonly learningService = inject(LearningService);
  private readonly recordService = inject(StudentExerciseRecordService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  private readonly chartContainerRef = viewChild<ElementRef<HTMLDivElement>>('curveChartContainer');
  private chartInstance: echarts.ECharts | null = null;

  readonly loading = signal(false);
  readonly dashboard = signal<LearningDashboardDto | null>(null);
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);
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

  /** 学习曲线总次数 */
  readonly totalCurveCount = computed(() =>
    this.learningCurve().reduce((s, p) => s + p.minutes, 0)
  );

  readonly inProgressCourses = computed<StudentCourseListItemDto[]>(() =>
    this.myCourses().filter(c => c.status === 1 || ((c.progress || 0) > 0 && (c.progress || 0) < 100))
  );

  readonly completedCourses = computed<StudentCourseListItemDto[]>(() =>
    this.myCourses().filter(c => c.status === 2 || (c.progress || 0) >= 100)
  );

  readonly notStartedCourses = computed<StudentCourseListItemDto[]>(() =>
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
        setTimeout(() => this.initLineChart(), 100);
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

  ngOnDestroy(): void {
    this.chartInstance?.dispose();
  }

  private initLineChart(): void {
    const container = this.chartContainerRef()?.nativeElement;
    if (!container) return;

    this.chartInstance?.dispose();
    this.chartInstance = echarts.init(container);

    const curve = this.learningCurve();
    if (curve.length === 0) return;

    const labels = curve.map(p => p.label);
    const values = curve.map(p => p.minutes);
    const maxVal = Math.max(...values, 1);

    this.chartInstance.setOption({
      tooltip: {
        trigger: 'axis',
        backgroundColor: '#fff',
        borderColor: '#e8ecf1',
        textStyle: { color: '#1e293b', fontSize: 13 },
        formatter: (params: any) => {
          const p = params[0];
          return `<strong>${p.axisValue}</strong><br/>学习活动：<b style="color:#1e6ce8">${p.value} 次</b>`;
        },
      },
      grid: { top: 30, right: 20, bottom: 30, left: 50 },
      xAxis: {
        type: 'category',
        data: labels,
        boundaryGap: false,
        axisLine: { lineStyle: { color: '#e2e8f0' } },
        axisTick: { show: false },
        axisLabel: { color: '#64748b', fontSize: 12 },
      },
      yAxis: {
        type: 'value',
        name: '次',
        nameTextStyle: { color: '#94a3b8', fontSize: 11 },
        min: 0,
        max: Math.ceil(maxVal * 1.2) || 10,
        minInterval: 1,
        splitLine: { lineStyle: { color: '#f1f5f9', type: 'dashed' } },
        axisLabel: { color: '#94a3b8', fontSize: 11 },
      },
      series: [{
        name: '学习活动',
        type: 'line',
        data: values,
        smooth: true,
        symbol: 'circle',
        symbolSize: 8,
        showSymbol: true,
        lineStyle: { color: '#1e6ce8', width: 2.5 },
        itemStyle: {
          color: '#1e6ce8',
          borderColor: '#fff',
          borderWidth: 2,
        },
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(30,108,232,0.18)' },
            { offset: 1, color: 'rgba(30,108,232,0.02)' },
          ]),
        },
        emphasis: {
          scale: 1.5,
          itemStyle: { shadowBlur: 8, shadowColor: 'rgba(30,108,232,0.4)' },
        },
      }],
    });
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
      { label: '习题练习', value: dash.totalExerciseRecords || 0, suffix: '次', icon: 'form', color: '#0891b2', hint: '已提交的习题' },
      { label: '资源学习', value: dash.totalResourceActivities || 0, suffix: '次', icon: 'folder-open', color: '#8b5cf6', hint: '预览/下载学习资料' },
    ]);
  }

  private buildLearningCurve(dash: LearningDashboardDto) {
    const labels = dash.dailyTimeLabels || [];
    const values = dash.dailyTimeValues || [];
    if (labels.length === 0) {
      this.learningCurve.set([]);
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
  courseGradient(course: StudentCourseListItemDto | { courseTitle?: string; courseId?: string; majorName?: string }): string {
    return this.gradientByKey(
      course?.courseTitle || course?.courseId || 'x',
      course?.majorName || ''
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

  hasCover(c: StudentCourseListItemDto): boolean {
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
