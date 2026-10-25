import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import * as echarts from 'echarts/core';
import { LineChart, PieChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { GridComponent, LegendComponent, TooltipComponent } from 'echarts/components';

import { LearningService } from '../../proxy/learning/learning.service';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import { StudentCourseStatus } from '../../proxy/learning/enums/student-course-status.enum';
import { SelfAssessment } from '../../proxy/learning/enums/self-assessment.enum';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import { ChartPalette, readChartPalette } from './chart-theme';
import {
  COURSE_STATUS_LABELS,
  RECORD_FILTER_LABELS,
  RECORD_FILTER_TO_IS_CORRECT,
  RECORD_RESULT_LABELS,
  RecordFilter,
  SELF_ASSESSMENT_LABELS,
} from './my-learning-labels';
import type {
  LearningDashboardDto,
  RecentLearningDto,
  StudentCourseListItemDto,
  StudentExerciseRecordDto,
} from '../../proxy/learning/dtos/models';

echarts.use([LineChart, PieChart, CanvasRenderer, TooltipComponent, GridComponent, LegendComponent]);

/** 顶部统计卡 */
interface StatCard {
  label: string;
  value: number;
  suffix: string;
  hint: string;
  route: string;
}

/** 学习曲线上的一个点 */
interface CurvePoint {
  label: string;
  value: number;
}

/**
 * 学生端「我的学习」。
 *
 * 数据全部来自真实接口：
 *   · LearningService.getDashboard()      → 学习曲线、活动构成、最近学习、统计
 *   · LearningService.getMyCourses()      → 我的课程（进行中课程）
 *   · StudentExerciseRecordService.getMyRecentRecords() → 习题记录（服务端分页）
 *
 * 本月目标不再展示：后端没有「学生目标」的数据源，页面不伪造目标值，
 * 改为如实说明「学习目标暂未开放」。
 */
@Component({
  selector: 'app-student-my-learning',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
  ],
  templateUrl: './student-my-learning.component.html',
  styleUrls: ['./student-my-learning.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyLearningComponent implements OnInit, OnDestroy {
  private readonly learningService = inject(LearningService);
  private readonly recordService = inject(StudentExerciseRecordService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  private readonly curveContainerRef = viewChild<ElementRef<HTMLDivElement>>('curveChartContainer');
  private readonly pieContainerRef = viewChild<ElementRef<HTMLDivElement>>('pieChartContainer');
  private chartInstance: echarts.ECharts | null = null;
  private pieChartInstance: echarts.ECharts | null = null;
  private chartTimer?: ReturnType<typeof setTimeout>;
  private chartResizeObserver: ResizeObserver | null = null;

  private readonly onWindowResize = (): void => {
    requestAnimationFrame(() => {
      this.chartInstance?.resize();
      this.pieChartInstance?.resize();
    });
  };

  // ══════════════ 状态 ══════════════

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly dashboard = signal<LearningDashboardDto | null>(null);
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);
  readonly coursesFailed = signal(false);

  readonly records = signal<StudentExerciseRecordDto[]>([]);
  readonly recordsLoading = signal(true);
  readonly recordsFailed = signal(false);
  readonly recordsTotal = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly recordFilter = signal<RecordFilter>('all');

  readonly learningCurve = signal<CurvePoint[]>([]);

  // ══════════════ 计算属性 ══════════════

  /** 顶部四张统计卡（全部来自学习总览接口） */
  readonly stats = computed<StatCard[]>(() => {
    const dash = this.dashboard();
    return [
      {
        label: '在学课程',
        value: dash?.totalCourses ?? 0,
        suffix: '门',
        hint: '已选课程总数',
        route: '/student/courses',
      },
      {
        label: '已完成课程',
        value: dash?.completedCourses ?? 0,
        suffix: '门',
        hint: '课程进度已达 100%',
        route: '/student/courses',
      },
      {
        label: '习题练习',
        value: dash?.totalExerciseRecords ?? 0,
        suffix: '次',
        hint: '已提交的习题次数',
        route: '/student/analysis',
      },
      {
        label: '资源学习',
        value: dash?.totalResourceActivities ?? 0,
        suffix: '次',
        hint: '预览 / 下载学习资料',
        route: '/student/resources',
      },
    ];
  });

  /** 学习曲线累计次数 */
  readonly totalCurveCount = computed(() =>
    this.learningCurve().reduce((sum, point) => sum + (point.value || 0), 0),
  );

  /** 学习活动构成：习题练习 / 资源学习（次数） */
  readonly activityBreakdown = computed(() => {
    const dash = this.dashboard();
    const exercises = dash?.totalExerciseRecords ?? 0;
    const resources = dash?.totalResourceActivities ?? 0;
    return { exercises, resources, total: exercises + resources };
  });

  /**
   * 进行中的课程。
   * 必须排除「已完成」与「已退课」——这两个状态也可能带 progress > 0，
   * 混进来会让标题「进行中的课程」与数据不符（已完成 / 已退课不属于进行中）。
   */
  readonly inProgressCourses = computed<StudentCourseListItemDto[]>(() =>
    this.myCourses().filter(
      c =>
        c.status !== StudentCourseStatus.Completed &&
        c.status !== StudentCourseStatus.Dropped &&
        (c.status === StudentCourseStatus.InProgress || (c.progress ?? 0) > 0) &&
        (c.progress ?? 0) < 100,
    ),
  );

  /** 最近学习 */
  readonly recentLearnings = computed<RecentLearningDto[]>(() => this.dashboard()?.recentLearning ?? []);

  /** 继续学习入口：进度最高的在学课程 */
  readonly continueCourse = computed<StudentCourseListItemDto | null>(() => {
    const list = this.inProgressCourses();
    if (!list.length) return null;
    return list.reduce((best, cur) => ((cur.progress ?? 0) > (best.progress ?? 0) ? cur : best), list[0]);
  });

  readonly continueLearningRoute = computed<string[]>(() => {
    const course = this.continueCourse();
    return course?.courseId ? ['/student/courses', course.courseId, 'learn'] : ['/student/courses'];
  });

  readonly continueCourseTitle = computed(() => this.continueCourse()?.courseTitle || '课程中心');

  /** 习题记录筛选标签（用于模板渲染页签） */
  readonly filterOptions: { key: RecordFilter; label: string }[] = (
    Object.keys(RECORD_FILTER_LABELS) as RecordFilter[]
  ).map(key => ({ key, label: RECORD_FILTER_LABELS[key] }));

  // ══════════════ 生命周期 ══════════════

  ngOnInit(): void {
    window.addEventListener('resize', this.onWindowResize);
    // 订阅式还原：浏览器前进/后退也能恢复筛选与页码
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.syncFromUrl(params);
    });
    this.loadDashboard();
    this.loadCourses();
    this.loadRecords();
  }

  ngOnDestroy(): void {
    if (this.chartTimer) {
      clearTimeout(this.chartTimer);
      this.chartTimer = undefined;
    }
    window.removeEventListener('resize', this.onWindowResize);
    this.chartResizeObserver?.disconnect();
    this.chartResizeObserver = null;
    this.chartInstance?.dispose();
    this.pieChartInstance?.dispose();
  }

  /** 整页重试 */
  reload(): void {
    this.loadFailed.set(false);
    this.loading.set(true);
    this.loadDashboard();
    this.loadCourses();
  }

  // ══════════════ 数据加载 ══════════════

  private loadDashboard(): void {
    this.learningService.getDashboard().subscribe({
      next: data => {
        this.dashboard.set(data);
        this.buildLearningCurve(data);
        this.loading.set(false);
        this.scheduleCharts();
      },
      error: () => {
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  private loadCourses(): void {
    this.coursesFailed.set(false);
    this.learningService.getMyCourses().subscribe({
      next: list => this.myCourses.set(list ?? []),
      error: () => {
        this.myCourses.set([]);
        this.coursesFailed.set(true);
      },
    });
  }

  private loadRecords(): void {
    this.recordsLoading.set(true);
    this.recordsFailed.set(false);
    const isCorrect = RECORD_FILTER_TO_IS_CORRECT[this.recordFilter()];
    this.recordService
      .getMyRecentRecords({
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
        isCorrect,
      })
      .subscribe({
        next: result => {
          this.records.set(result?.items ?? []);
          this.recordsTotal.set(result?.totalCount ?? 0);
          this.recordsLoading.set(false);
        },
        error: () => {
          this.records.set([]);
          this.recordsTotal.set(0);
          this.recordsFailed.set(true);
          this.recordsLoading.set(false);
        },
      });
  }

  private buildLearningCurve(dash: LearningDashboardDto): void {
    const labels = dash?.dailyTimeLabels ?? [];
    const values = dash?.dailyTimeValues ?? [];
    this.learningCurve.set(labels.map((label, index) => ({ label, value: values[index] ?? 0 })));
  }

  // ══════════════ 筛选 / 分页（写进 URL） ══════════════

  private readFilterFromUrl(params: ParamMap): RecordFilter {
    const value = params.get('result');
    return value === 'correct' || value === 'wrong' ? value : 'all';
  }

  /** 从 URL 还原筛选与页码（订阅式，浏览器前进/后退同样生效） */
  private syncFromUrl(params: ParamMap): void {
    const filter = this.readFilterFromUrl(params);
    const page = Number(params.get('page') ?? '1');
    const changed = filter !== this.recordFilter() || page !== this.pageIndex();
    this.recordFilter.set(filter);
    this.pageIndex.set(isNaN(page) || page < 1 ? 1 : page);
    if (changed) this.loadRecords();
  }

  /** 切换习题记录筛选：写回 URL 并重新查询（服务端过滤） */
  setRecordFilter(filter: RecordFilter): void {
    if (filter === this.recordFilter()) return;
    this.recordFilter.set(filter);
    this.pageIndex.set(1);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { result: filter === 'all' ? null : filter },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    this.loadRecords();
  }

  /** 服务端分页：页码变化重新查询 */
  onPageChange(event: { pageIndex: number; pageSize: number }): void {
    this.pageSize.set(event.pageSize);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: event.pageIndex },
      queryParamsHandling: 'merge',
    });
  }

  /** 习题记录重试 */
  reloadRecords(): void {
    this.loadRecords();
  }

  // ══════════════ 图表 ══════════════

  private scheduleCharts(): void {
    if (this.chartTimer) clearTimeout(this.chartTimer);
    this.chartTimer = setTimeout(() => {
      this.chartTimer = undefined;
      this.initLineChart();
      this.initPieChart();
    }, 0);
  }

  private observeChartResize(container: HTMLElement): void {
    this.chartResizeObserver?.disconnect();
    if (typeof ResizeObserver === 'undefined') return;
    this.chartResizeObserver = new ResizeObserver(() => {
      this.chartInstance?.resize();
      this.pieChartInstance?.resize();
    });
    this.chartResizeObserver.observe(container);
  }

  /** 学习曲线（折线） */
  private initLineChart(): void {
    const container = this.curveContainerRef()?.nativeElement;
    const points = this.learningCurve();
    if (!container || !points.length) return;

    const palette = readChartPalette();
    if (!palette.accent) return;

    this.chartInstance?.dispose();
    this.chartInstance = echarts.init(container);

    const values = points.map(p => p.value);
    const maxValue = Math.max(...values, 1);

    this.chartInstance.setOption({
      animation: false,
      grid: { top: 24, right: 24, bottom: 28, left: 44 },
      tooltip: {
        trigger: 'axis',
        backgroundColor: palette.panel,
        borderColor: palette.line,
        textStyle: { color: palette.ink, fontSize: 13 },
        formatter: (params: unknown) => {
          const list = params as { axisValue: string; value: number }[];
          const first = list?.[0];
          if (!first) return '';
          return `<strong>${first.axisValue}</strong><br/>学习活动：<b>${first.value} 次</b>`;
        },
      },
      xAxis: {
        type: 'category',
        data: points.map(p => p.label),
        boundaryGap: false,
        axisLine: { lineStyle: { color: palette.line } },
        axisTick: { show: false },
        axisLabel: { color: palette.muted, fontSize: 12 },
      },
      yAxis: {
        type: 'value',
        name: '次',
        nameTextStyle: { color: palette.muted, fontSize: 11 },
        min: 0,
        max: Math.ceil(maxValue * 1.2) || 10,
        minInterval: 1,
        splitLine: { lineStyle: { color: palette.line, type: 'dashed' } },
        axisLabel: { color: palette.muted, fontSize: 11 },
      },
      series: [
        {
          name: '学习活动',
          type: 'line',
          data: values,
          smooth: true,
          symbol: 'circle',
          symbolSize: 8,
          lineStyle: { color: palette.accent, width: 2.5 },
          itemStyle: { color: palette.accent, borderColor: palette.panel, borderWidth: 2 },
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: palette.accentSoft },
              { offset: 1, color: palette.panel },
            ]),
          },
        },
      ],
    });
  }

  /** 学习活动构成（环形图） */
  private initPieChart(): void {
    const container = this.pieContainerRef()?.nativeElement;
    if (!container) return;

    const { exercises, resources, total } = this.activityBreakdown();
    if (total === 0) return;

    const palette = readChartPalette();
    if (!palette.accent) return;

    this.pieChartInstance?.dispose();
    this.pieChartInstance = echarts.init(container);
    this.observeChartResize(container);

    this.pieChartInstance.setOption({
      animation: false,
      tooltip: {
        trigger: 'item',
        backgroundColor: palette.panel,
        borderColor: palette.line,
        textStyle: { color: palette.ink, fontSize: 13 },
        formatter: (p: unknown) => {
          const item = p as { name: string; value: number; percent: number };
          return `<strong>${item.name}</strong><br/>${item.value} 次（${item.percent}%）`;
        },
      },
      legend: {
        bottom: 0,
        icon: 'circle',
        itemWidth: 8,
        itemHeight: 8,
        itemGap: 18,
        textStyle: { color: palette.muted, fontSize: 12 },
      },
      series: [
        {
          name: '学习活动',
          type: 'pie',
          radius: ['44%', '64%'],
          center: ['50%', '44%'],
          avoidLabelOverlap: true,
          itemStyle: { borderColor: palette.panel, borderWidth: 3, borderRadius: 4 },
          label: {
            show: true,
            formatter: '{b}\n{c} 次',
            color: palette.muted,
            fontSize: 12,
            lineHeight: 16,
          },
          labelLine: { show: true, smooth: true, length: 10, length2: 12 },
          data: [
            { value: exercises, name: '习题练习', itemStyle: { color: palette.accent } },
            { value: resources, name: '资源学习', itemStyle: { color: palette.gold || palette.accent } },
          ],
        },
      ],
    });
  }

  // ══════════════ 展示辅助 ══════════════

  /** 课程状态标签 */
  statusLabel(status?: StudentCourseStatus): string {
    if (status === undefined || status === null) return COURSE_STATUS_LABELS[StudentCourseStatus.Enrolled];
    return COURSE_STATUS_LABELS[status] ?? COURSE_STATUS_LABELS[StudentCourseStatus.Enrolled];
  }

  /** 习题记录判定标签 */
  resultLabel(isCorrect?: boolean | null): string {
    if (isCorrect === true) return RECORD_RESULT_LABELS.correct;
    if (isCorrect === false) return RECORD_RESULT_LABELS.wrong;
    return RECORD_RESULT_LABELS.unknown;
  }

  /** 自评标签 */
  selfAssessmentLabel(value?: number | null): string {
    if (value === undefined || value === null) return SELF_ASSESSMENT_LABELS[SelfAssessment.None];
    return SELF_ASSESSMENT_LABELS[value as SelfAssessment] ?? SELF_ASSESSMENT_LABELS[SelfAssessment.None];
  }

  /** 课程副标题：专业 · 进度 · 习题 */
  courseSubtitle(course: StudentCourseListItemDto): string {
    const parts = [course.majorName || '公共课', `进度 ${course.progress ?? 0}%`];
    if (course.totalExerciseCount) {
      parts.push(`习题 ${course.completedExerciseCount ?? 0}/${course.totalExerciseCount}`);
    }
    return parts.join(' · ');
  }

  /** 课程封面渐变（只用设计令牌派生，不硬编码色值） */
  courseGradient(index: number): string {
    const palette = readChartPalette();
    const sets = [
      `linear-gradient(140deg, ${palette.accentLight ?? palette.accent}, ${palette.accent})`,
      `linear-gradient(140deg, ${palette.accent}, ${palette.accentDeep})`,
      `linear-gradient(140deg, ${palette.gold}, ${palette.accentDeep})`,
      `linear-gradient(140deg, ${palette.accentDeep}, ${palette.ink})`,
    ];
    return sets[index % sets.length];
  }

  /** 课程封面是否有真实图片 */
  hasCover(course: StudentCourseListItemDto): boolean {
    return !!course.courseCoverImageUrl && course.courseCoverImageUrl.trim().length > 0;
  }

  /** 跳转到课程详情 */
  openCourse(courseId?: string): void {
    if (!courseId) return;
    this.router.navigate(['/student/courses', courseId]);
  }

  /** 跳转到课程中心 */
  goCourses(): void {
    this.router.navigate(['/student/courses']);
  }
}
