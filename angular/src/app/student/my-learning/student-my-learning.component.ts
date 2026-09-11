import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, computed, inject, signal, viewChild, ElementRef } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import * as echarts from 'echarts/core';
import { LineChart, PieChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent, GridComponent, LegendComponent } from 'echarts/components';
import { CourseService } from '../../proxy/courses/course.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';
import { hashGradient } from '../../shared/utils/color.util';

echarts.use([LineChart, PieChart, CanvasRenderer, TooltipComponent, GridComponent, LegendComponent]);
import type { LearningDashboardDto, StudentCourseListItemDto, RecentLearningDto } from '../../proxy/learning/dtos/models';

interface StatItem {
  label: string;
  value: number;
  suffix: string;
  icon: string;
  color: string;
  hint?: string;
}

interface GoalItem {
  label: string;
  current: number;
  target: number;
  percent: number;
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
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzProgressModule,
    StudentHeroComponent,
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
  private readonly pieContainerRef = viewChild<ElementRef<HTMLDivElement>>('pieChartContainer');
  private chartInstance: echarts.ECharts | null = null;
  private pieChartInstance: echarts.ECharts | null = null;
  private chartTimer?: ReturnType<typeof setTimeout>;

  private readonly onWindowResize = () => {
    requestAnimationFrame(() => {
      this.chartInstance?.resize();
      this.pieChartInstance?.resize();
    });
  };

  readonly loading = signal(false);
  readonly dashboard = signal<LearningDashboardDto | null>(null);
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);
  readonly records = signal<ExerciseRecordItem[]>([]);
  readonly recordsLoading = signal(false);

  /** 学习曲线（用最近 7 天） */
  readonly learningCurve = signal<DailyPoint[]>([]);

  readonly stats = signal<StatItem[]>([
    { label: '总课程数', value: 0, suffix: '门', icon: 'book', color: '#2b6cd4' },
    { label: '已完成', value: 0, suffix: '门', icon: 'check-circle', color: '#10b981' },
    { label: '总学时', value: 0, suffix: 'h', icon: 'clock-circle', color: '#06b6d4' },
    { label: '平均进度', value: 0, suffix: '%', icon: 'rise', color: '#1f56ad' },
  ]);

  /** 学习曲线总次数 */
  readonly totalCurveCount = computed(() =>
    this.learningCurve().reduce((s, p) => s + p.minutes, 0)
  );

  /** 学习活动构成：习题练习 / 资源学习（均为次数） */
  readonly activityBreakdown = computed(() => {
    const dash = this.dashboard();
    const exercises = dash?.totalExerciseRecords || 0;
    const resources = dash?.totalResourceActivities || 0;
    return { exercises, resources, total: exercises + resources };
  });

  readonly inProgressCourses = computed<StudentCourseListItemDto[]>(() =>
    this.myCourses().filter(c => c.status === 1 || ((c.progress || 0) > 0 && (c.progress || 0) < 100))
  );

  readonly recentLearnings = computed<RecentLearningDto[]>(() => {
    return this.dashboard()?.recentLearning || [];
  });

  /** 本月目标：目标值固定，完成值取真实学习统计 */
  readonly monthlyGoals = computed<GoalItem[]>(() => {
    const dash = this.dashboard();
    const completedCourses = dash?.completedCourses || 0;
    const totalExercises = dash?.totalExerciseRecords || 0;
    const courseTarget = 3;
    const exerciseTarget = 200;
    return [
      {
        label: `完成 ${courseTarget} 门课程`,
        current: completedCourses,
        target: courseTarget,
        percent: Math.min(100, Math.round((completedCourses / courseTarget) * 100)),
      },
      {
        label: `完成 ${exerciseTarget} 道习题`,
        current: totalExercises,
        target: exerciseTarget,
        percent: Math.min(100, Math.round((totalExercises / exerciseTarget) * 100)),
      },
    ];
  });

  ngOnInit() {
    window.addEventListener('resize', this.onWindowResize);
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
        if (this.chartTimer) clearTimeout(this.chartTimer);
        this.chartTimer = setTimeout(() => {
          this.chartTimer = undefined;
          this.initLineChart();
          this.initPieChart();
        }, 0);
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
    if (this.chartTimer) {
      clearTimeout(this.chartTimer);
      this.chartTimer = undefined;
    }
    window.removeEventListener('resize', this.onWindowResize);
    this.chartInstance?.dispose();
    this.pieChartInstance?.dispose();
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
      animation: false,
      tooltip: {
        trigger: 'axis',
        backgroundColor: '#fff',
        borderColor: '#e8ecf1',
        textStyle: { color: '#1e293b', fontSize: 13 },
        formatter: (params: any) => {
          const p = params[0];
          return `<strong>${p.axisValue}</strong><br/>学习活动：<b style="color:#2b6cd4">${p.value} 次</b>`;
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
        lineStyle: { color: '#2b6cd4', width: 2.5 },
        itemStyle: {
          color: '#2b6cd4',
          borderColor: '#fff',
          borderWidth: 2,
        },
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(43, 108, 212,0.18)' },
            { offset: 1, color: 'rgba(43, 108, 212,0.02)' },
          ]),
        },
        emphasis: {
          itemStyle: { shadowBlur: 6, shadowColor: 'rgba(43, 108, 212,0.35)' },
        },
      }],
    });
  }

  /** 学习活动构成环形图（习题练习 / 资源学习） */
  private initPieChart(): void {
    const container = this.pieContainerRef()?.nativeElement;
    if (!container) return;

    this.pieChartInstance?.dispose();
    this.pieChartInstance = echarts.init(container);

    const { exercises, resources, total } = this.activityBreakdown();
    if (total === 0) return;

    this.pieChartInstance.setOption({
      animation: false,
      tooltip: {
        trigger: 'item',
        backgroundColor: '#fff',
        borderColor: '#e8ecf1',
        textStyle: { color: '#1e293b', fontSize: 13 },
        formatter: (p: any) => `<strong>${p.name}</strong><br/>${p.value} 次（${p.percent}%）`,
      },
      legend: {
        bottom: 4,
        icon: 'circle',
        itemWidth: 8,
        itemHeight: 8,
        itemGap: 18,
        textStyle: { color: '#64748b', fontSize: 12 },
      },
      title: {
        text: String(total),
        subtext: '总活动',
        left: 'center',
        top: '38%',
        textAlign: 'center',
        textStyle: { color: '#1e293b', fontSize: 24, fontWeight: 700 },
        subtextStyle: { color: '#94a3b8', fontSize: 12 },
      },
      series: [{
        name: '学习活动',
        type: 'pie',
        radius: ['44%', '64%'],
        center: ['50%', '46%'],
        avoidLabelOverlap: true,
        itemStyle: { borderColor: '#fff', borderWidth: 3, borderRadius: 4 },
        label: {
          show: true,
          formatter: '{b}\n{c} 次',
          color: '#475569',
          fontSize: 12,
          lineHeight: 16,
        },
        labelLine: { show: true, smooth: true, length: 10, length2: 12 },
        data: [
          { value: exercises, name: '习题练习', itemStyle: { color: '#2b6cd4' } },
          { value: resources, name: '资源学习', itemStyle: { color: '#10b981' } },
        ],
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
      { label: '总课程数', value: dash.totalCourses || 0, suffix: '门', icon: 'book', color: '#2b6cd4' },
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
      '#1f56ad',
      '#2b6cd4',
      '#2b6cd4',
      '#0891b2',
      '#2b6cd4',
      '#059669',
      '#10b981',
      '#0e7490',
    ];
    const key = (primary || 'x') + (secondary || '');
    return hashGradient(key, palettes);
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
      1: '#2b6cd4',
      2: '#06b6d4',
      3: '#10b981',
    };
    return map[s || 0] || '#2b6cd4';
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
