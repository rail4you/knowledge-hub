import { Component, signal, inject, OnInit, OnDestroy, ChangeDetectionStrategy, computed, ElementRef, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as echarts from 'echarts/core';
import { BarChart, PieChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent, LegendComponent, GridComponent } from 'echarts/components';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';
import { formatTrueFalseAnswer } from '../../shared/utils/exercise-answer.util';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import {
  StudentLearningStatisticsDto,
  CourseLearningOverviewDto,
  CourseStatisticsItemDto,
  GetLearningStatisticsInput,
  GetStudentLearningDetailInput,
  StudentLearningDetailDto,
  TenantCourseStatisticsDto,
} from '../../proxy/learning/dtos/models';

echarts.use([BarChart, PieChart, CanvasRenderer, TooltipComponent, LegendComponent, GridComponent]);

@Component({
  selector: 'app-learning-statistics',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzTableModule,
    NzIconModule,
    NzSpinModule,
    NzTagModule,
    NzTooltipModule,
    NzEmptyModule,
    NzInputModule,
    NzTabsModule,
    NzModalModule,
  ],
  templateUrl: './learning-statistics.component.html',
  styleUrls: ['./learning-statistics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LearningStatisticsComponent implements OnInit, OnDestroy {
  private readonly recordService = inject(StudentExerciseRecordService);
  private readonly courseService = inject(CourseService);
  private readonly message = inject(NzMessageService);

  // ===== 顶层 Tab：0=总体统计，1=课程明细 =====
  mainTab = signal(0);

  // ===== 总体统计（租户级全课程汇总） =====
  loadingTenant = signal(false);
  tenantStats = signal<TenantCourseStatisticsDto | null>(null);
  readonly tenantCourses = computed(() => this.tenantStats()?.courses ?? []);

  private readonly rateBarRef = viewChild<ElementRef<HTMLDivElement>>('rateBarChart');
  private readonly studentPieRef = viewChild<ElementRef<HTMLDivElement>>('studentPieChart');
  private rateBarChart: echarts.ECharts | null = null;
  private studentPieChart: echarts.ECharts | null = null;

  // ===== 左侧课程列表 =====
  loadingCourses = signal(false);
  courses = signal<CourseDto[]>([]);
  courseSearchText = signal('');

  readonly filteredCourses = computed(() => {
    const kw = this.courseSearchText().trim().toLowerCase();
    const all = this.courses();
    if (!kw) return all;
    return all.filter(
      c =>
        (c.title ?? '').toLowerCase().includes(kw) ||
        ((c as CourseDto).majorName ?? '').toLowerCase().includes(kw),
    );
  });

  selectedCourseId = signal<string | null>(null);
  selectedCourse = computed(() => {
    const id = this.selectedCourseId();
    if (!id) return null;
    return this.courses().find(c => c.id === id) ?? null;
  });

  // ===== 右侧 Tab 状态：0=学生统计，1=章节统计 =====
  activeTab = signal(0);

  // ===== 导出（总体统计：各课程汇总表） =====
  selectedChapterId: string | null = null;
  exporting = signal(false);

  // ===== 概览 + 明细 =====
  loadingOverview = signal(false);
  overview = signal<CourseLearningOverviewDto | null>(null);

  loading = signal(false);
  statistics = signal<StudentLearningStatisticsDto[]>([]);
  totalCount = signal(0);
  pageIndex = signal(1);
  pageSize = signal(10);

  // ===== 章节进度概览分页（前端分页，由 nz-table 接管切片） =====
  chapterPageIndex = signal(1);
  chapterPageSize = signal(10);

  // ===== 学生学习详情弹窗 =====
  detailVisible = signal(false);
  detailLoading = signal(false);
  studentDetail = signal<StudentLearningDetailDto | null>(null);
  // 章节明细展开的章节 ID 集合
  expandedChapters = signal<Set<string>>(new Set());
  readonly detailChapters = computed(() => this.studentDetail()?.chapters ?? []);

  // 后端已只返回有习题关联的章节，前端再兜底过滤一次，避免旧接口缓存出现空行
  readonly chapterProgressList = computed(() =>
    (this.overview()?.chapterProgress ?? []).filter(c => (c.totalExercises ?? 0) > 0));
  readonly chapterTotal = computed(() => this.chapterProgressList().length);
  readonly pagedChapterProgress = computed(() => {
    const all = this.chapterProgressList();
    const start = (this.chapterPageIndex() - 1) * this.chapterPageSize();
    return all.slice(start, start + this.chapterPageSize());
  });

  ngOnInit() {
    this.loadCourses();
    this.loadTenantStatistics();
  }

  ngOnDestroy() {
    this.rateBarChart?.dispose();
    this.rateBarChart = null;
    this.studentPieChart?.dispose();
    this.studentPieChart = null;
  }

  onMainTabChange(index: number) {
    this.mainTab.set(index);
    // 图表容器在隐藏 Tab 下宽度为 0，切换回来后重建
    if (index === 0 && this.tenantStats()) {
      setTimeout(() => this.initOverviewCharts());
    }
  }

  // ===== 总体统计 =====
  loadTenantStatistics() {
    this.loadingTenant.set(true);
    this.recordService.getTenantCourseStatistics({}).subscribe({
      next: data => {
        this.tenantStats.set(data);
        this.loadingTenant.set(false);
        setTimeout(() => this.initOverviewCharts());
      },
      error: () => {
        this.loadingTenant.set(false);
        this.message.error('加载总体统计失败');
      },
    });
  }

  viewCourseDetail(courseId: string | undefined) {
    if (!courseId) return;
    this.mainTab.set(1);
    this.selectCourse(courseId);
  }

  private initOverviewCharts() {
    const barEl = this.rateBarRef();
    const pieEl = this.studentPieRef();
    const courses = this.tenantCourses();
    if (!barEl || !pieEl || courses.length === 0) return;
    this.rateBarChart?.dispose();
    this.studentPieChart?.dispose();
    this.rateBarChart = echarts.init(barEl.nativeElement);
    this.studentPieChart = echarts.init(pieEl.nativeElement);
    this.updateOverviewCharts();
  }

  private updateOverviewCharts() {
    const courses = this.tenantCourses();
    if (courses.length === 0) return;
    const names = courses.map(c => this.truncate(c.courseName ?? '', 10));
    const fullNames = courses.map(c => c.courseName ?? '');

    this.rateBarChart?.setOption({
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' },
        backgroundColor: 'rgba(255,255,255,0.96)',
        borderColor: '#c4d4cc',
        borderWidth: 1,
        extraCssText: 'box-shadow: 0 6px 18px rgba(24,52,68,0.10); border-radius: 6px;',
        textStyle: { color: '#183444', fontSize: 12 },
        formatter: (params: any) => {
          const p = Array.isArray(params) ? params[0] : params;
          const d = p?.data ?? {};
          return `<div style="font-weight:600;margin-bottom:4px;">${d.courseName ?? p?.name ?? ''}</div>
            <div>学习人数：<b style="color:#0c7560">${d.value ?? 0}</b> 人</div>
            <div>完成率：<b style="color:#1f9c75">${d.completionRate ?? 0}%</b></div>
            <div>正确率：<b style="color:#1f9c75">${d.correctRate ?? 0}%</b></div>`;
        },
      },
      grid: { left: 8, right: 8, bottom: 0, top: 32, containLabel: true },
      xAxis: {
        type: 'category',
        data: names,
        axisLabel: { interval: 0, rotate: names.length > 6 ? 30 : 0, fontSize: 12, color: '#697e88' },
        axisLine: { lineStyle: { color: '#e1e7e0' } },
        axisTick: { show: false },
      },
      yAxis: {
        type: 'value',
        name: '学习人数',
        nameTextStyle: { color: '#697e88', fontSize: 12 },
        minInterval: 1,
        axisLabel: { color: '#697e88' },
        splitLine: { lineStyle: { color: '#edf0eb', type: 'dashed' } },
      },
      series: [
        {
          name: '学习人数',
          type: 'bar',
          barMaxWidth: 44,
          itemStyle: {
            borderRadius: [6, 6, 0, 0],
            color: {
              type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: '#1f9c75' },
                { offset: 1, color: '#0c7560' },
              ],
            },
          },
          label: { show: true, position: 'top', fontSize: 12, color: '#3d5563' },
          data: courses.map((c, i) => ({
            value: c.totalStudents ?? 0,
            courseName: fullNames[i],
            completionRate: c.averageCompletionRate ?? 0,
            correctRate: c.averageCorrectRate ?? 0,
          })),
        },
      ],
    });

    // 各课程学习人数占比 ─ 环形图 + 中心总数 + 绿色配色 + 学习人数降序
    const sortedCourses = [...courses].sort(
      (a, b) => (b.totalStudents ?? 0) - (a.totalStudents ?? 0),
    );
    const totalStudentsAll = sortedCourses.reduce(
      (sum, c) => sum + (c.totalStudents ?? 0),
      0,
    );
    const pieData = sortedCourses.map((c, i) => ({
      name: this.truncate(c.courseName ?? '未命名课程', 14),
      fullName: c.courseName ?? '',
      courseId: c.courseId,
      value: c.totalStudents ?? 0,
      _idx: i,
      _rate: totalStudentsAll > 0
        ? +(((c.totalStudents ?? 0) / totalStudentsAll) * 100).toFixed(1)
        : 0,
    }));

    this.studentPieChart?.setOption({
      tooltip: {
        trigger: 'item',
        backgroundColor: 'rgba(255,255,255,0.96)',
        borderColor: '#e1e7e0',
        borderWidth: 1,
        textStyle: { color: '#183444', fontSize: 12 },
        extraCssText: 'box-shadow: 0 6px 18px rgba(24,52,68,0.10); border-radius: 6px;',
        formatter: (params: any) => {
          const d = params?.data ?? {};
          const value = d.value ?? 0;
          const pct = params?.percent ?? 0;
          return `<div style="font-weight:600;margin-bottom:4px;">${d.fullName ?? d.name}</div>
            <div>学习人数：<b style="color:#0c7560">${value}</b> 人</div>
            <div>占比：<b style="color:#1f9c75">${pct.toFixed(1)}%</b></div>`;
        },
      },
      legend: {
        type: 'scroll',
        bottom: 0,
        left: 0,
        right: 0,
        icon: 'circle',
        itemWidth: 8,
        itemHeight: 8,
        itemGap: 14,
        textStyle: { fontSize: 12, color: '#3d5563' },
        formatter: (name: string) => {
          const item = pieData.find(d => d.name === name);
          if (!item) return name;
          return `${name}  ${item.value}人  ${item._rate}%`;
        },
      },
      series: [{
        name: '学习人数占比',
        type: 'pie',
        radius: ['38%', '58%'],
        center: ['50%', '42%'],
        avoidLabelOverlap: true,
        minMarginAngle: 6,
        // 深度绿色调色板（人位绿 ~ 草绿），起点是接近品牌主色的深青绿
        color: this.pieColors(pieData.length),
        itemStyle: {
          borderRadius: 4,
          borderColor: '#fff',
          borderWidth: 2,
        },
        // 外侧标签 + 引线：所有标签统一在同一外圆上视觉对齐，
        // 避免“内部标签在每个扇区中心、感党不均”的问题；同时去掉 <5% 隐藏造成的“空白”。
        label: {
          show: true,
          position: 'outside',
          alignTo: 'labelLine',
          edgeDistance: 8,
          distanceToLabelLine: 4,
          formatter: (p: any) => {
            const pct = p?.percent ?? 0;
            const d = p?.data ?? {};
            // 极小的扇区不画标签，避免与邻近标签挤压（信息全部在图例中可看）
            if (pct < 3) return '';
            const value = d.value ?? 0;
            return `{n|${p.name}}\n{v|${value}人  ${pct.toFixed(0)}%}`;
          },
          rich: {
            n: { fontSize: 12, color: '#3d5563', lineHeight: 16, fontWeight: 500 },
            v: { fontSize: 12, color: '#0c7560', lineHeight: 16, fontWeight: 700 },
          },
        },
        labelLine: {
          show: true,
          length: 8,
          length2: 10,
          lineStyle: { color: '#c4d4cc', width: 1 },
        },
        labelLayout: {
          hideOverlap: true,
          moveOverlap: 'shiftY',
        },
        emphasis: {
          scale: true,
          scaleSize: 6,
          itemStyle: {
            shadowBlur: 16,
            shadowColor: 'rgba(12,117,96,0.40)',
          },
        },
        select: {
          itemStyle: { borderColor: '#0c7560', borderWidth: 2 },
        },
        data: pieData,
      }],
      // 中心标题：依赖 graphic 子元素渲染时与系列同步缩放
      graphic: [
        {
          type: 'text',
          left: 'center',
          top: '38%',
          style: {
            text: '总学习人数',
            fill: '#8a99a1',
            fontSize: 12,
            fontWeight: 400,
          },
        },
        {
          type: 'text',
          left: 'center',
          top: '46%',
          style: {
            text: `${totalStudentsAll}`,
            fill: '#0c7560',
            fontSize: 26,
            fontWeight: 700,
            fontFamily: 'inherit',
          },
        },
        {
          type: 'text',
          left: 'center',
          top: '54%',
          style: {
            text: `共 ${pieData.length} 门课程`,
            fill: '#697e88',
            fontSize: 11,
          },
        },
      ],
    });
  }

  private truncate(text: string, max: number): string {
    return text.length > max ? `${text.slice(0, max)}…` : text;
  }

  loadCourses() {
    this.loadingCourses.set(true);
    this.courseService.getList({ skipCount: 0, maxResultCount: 100 } as any).subscribe({
      next: result => {
        this.courses.set(result.items ?? []);
        this.loadingCourses.set(false);
      },
      error: () => {
        this.loadingCourses.set(false);
        this.message.error('加载课程列表失败');
      },
    });
  }

  selectCourse(courseId: string) {
    if (this.selectedCourseId() === courseId) return;
    this.selectedCourseId.set(courseId);
    this.pageIndex.set(1);
    this.chapterPageIndex.set(1);
    this.loadOverview();
    this.loadStatistics();
  }

  loadOverview() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loadingOverview.set(true);
    this.recordService.getCourseLearningOverview({ courseId }).subscribe({
      next: data => {
        this.overview.set(data);
        this.chapterPageIndex.set(1);
        this.loadingOverview.set(false);
      },
      error: () => {
        this.loadingOverview.set(false);
        this.message.error('加载概览失败');
      },
    });
  }

  loadStatistics() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    const input: GetLearningStatisticsInput = {
      courseId,
      chapterId: this.selectedChapterId,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    };

    this.recordService.getLearningStatistics(input).subscribe({
      next: result => {
        this.statistics.set(result.items ?? []);
        this.totalCount.set(result.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => {
        this.message.error('加载统计数据失败');
        this.loading.set(false);
      },
    });
  }

  onPageChange(page: number) {
    this.pageIndex.set(page);
    this.loadStatistics();
  }

  onPageSizeChange(size: number) {
    this.pageSize.set(size);
    this.pageIndex.set(1);
    this.loadStatistics();
  }

  onChapterPageChange(page: number) {
    this.chapterPageIndex.set(page);
  }

  onChapterPageSizeChange(size: number) {
    this.chapterPageSize.set(size);
    this.chapterPageIndex.set(1);
  }

  // ===== 学生学习详情弹窗 =====
  openStudentDetail(student: StudentLearningStatisticsDto) {
    const courseId = this.selectedCourseId();
    if (!courseId || !student.studentId) return;

    this.detailVisible.set(true);
    this.detailLoading.set(true);
    this.studentDetail.set(null);
    this.expandedChapters.set(new Set());

    const input: GetStudentLearningDetailInput = {
      courseId,
      studentId: student.studentId,
    };
    this.recordService.getStudentLearningDetail(input).subscribe({
      next: data => {
        this.studentDetail.set(data);
        this.detailLoading.set(false);
      },
      error: () => {
        this.detailLoading.set(false);
        this.message.error('加载学生详情失败');
      },
    });
  }

  closeStudentDetail() {
    this.detailVisible.set(false);
  }

  isChapterExpanded(chapterId: string | undefined): boolean {
    return !!chapterId && this.expandedChapters().has(chapterId);
  }

  toggleChapterExpanded(chapterId: string | undefined) {
    if (!chapterId) return;
    const set = new Set(this.expandedChapters());
    if (set.has(chapterId)) {
      set.delete(chapterId);
    } else {
      set.add(chapterId);
    }
    this.expandedChapters.set(set);
  }

  getRecordStatus(
    isCorrect: boolean | null | undefined,
  ): { label: string; color: string } {
    if (isCorrect === true) return { label: '答对', color: 'green' };
    if (isCorrect === false) return { label: '答错', color: 'red' };
    return { label: '待批改', color: 'gold' };
  }

  /**
   * 作答文本：判断题显示“正确/错误”，其他题型原样。
   * 判断题的 StudentAnswer 存的是 "true"/"false"，直接展示会看到英文布尔值。
   */
  formatAnswer(record: { studentAnswer?: string | null; exerciseType?: ExerciseType }): string {
    return formatTrueFalseAnswer(record.studentAnswer, record.exerciseType === ExerciseType.TrueFalse);
  }

  openExportModal() {
    this.exportOverview();
  }

  // 总体统计：导出各课程汇总表
  exportOverview() {
    this.exporting.set(true);
    this.recordService.exportTenantCourseStatistics({}).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `课程统计_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.exporting.set(false);
        this.message.success('导出成功');
      },
      error: () => {
        this.message.error('导出失败');
        this.exporting.set(false);
      },
    });
  }

  // 保留旧方法名兼容模板误调用
  exportExcel() {
    this.exportOverview();
  }

  /**
   * 深度绿色渐变主题：占比最高的扇区为品牌深青绿 hsl(165, 78%, 26%),
   * 依次递变为草绿 hsl(132, 55%, 56%)。色环较窄只走向正绿，明度区间更窄，
   * 保证顶端色块「沉」得下去，后面几级仍可一眼分辨，同时头部在人位绿基础上色调更纯。
   * 6门课程为例：hsl(165,78%,26%) → hsl(156,70%,33%) → hsl(148,62%,40%)
   *                     hsl(140,58%,47%) → hsl(136,55%,52%) → hsl(132,55%,57%)
   * 单课：取品牌主色 #0c7560 。
   */
  private pieColors(count: number): string[] {
    if (count <= 0) return [];
    if (count === 1) return ['#0c7560'];

    const startH = 165;   // 深青绿起点（偏品牌色）
    const endH = 132;     // 草绿终点（纯绿）
    const startS = 78;
    const endS = 55;
    const startL = 26;    // 顶端亮度更深，更沉
    const endL = 57;

    const colors: string[] = [];
    for (let i = 0; i < count; i++) {
      const t = count === 1 ? 0 : i / (count - 1);
      const h = startH + (endH - startH) * t;
      const s = Math.round(startS + (endS - startS) * t);
      const l = Math.round(startL + (endL - startL) * t);
      colors.push(`hsl(${h.toFixed(0)}, ${s}%, ${l}%)`);
    }
    return colors;
  }

  getScoreColor(rate: number): string {
    if (rate >= 80) return '#52c41a';
    if (rate >= 60) return '#faad14';
    return '#ff4d4f';
  }

  getCompletionColor(rate: number): string {
    if (rate >= 80) return 'green';
    if (rate >= 50) return 'gold';
    return 'red';
  }

  getCorrectColor(rate: number): string {
    if (rate >= 70) return 'green';
    if (rate >= 40) return 'gold';
    return 'red';
  }
}
