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
        formatter: (params: any) => {
          const p = Array.isArray(params) ? params[0] : params;
          const d = p?.data ?? {};
          return `${d.courseName ?? p?.name ?? ''}<br/>学习人数：${d.value ?? 0}<br/>完成率：${d.completionRate ?? 0}%<br/>正确率：${d.correctRate ?? 0}%`;
        },
      },
      grid: { left: 8, right: 8, bottom: 0, top: 32, containLabel: true },
      xAxis: {
        type: 'category',
        data: names,
        axisLabel: { interval: 0, rotate: names.length > 6 ? 30 : 0, fontSize: 12 },
      },
      yAxis: { type: 'value', name: '学习人数', minInterval: 1 },
      series: [
        {
          name: '学习人数',
          type: 'bar',
          barMaxWidth: 44,
          itemStyle: { color: '#1677ff', borderRadius: [4, 4, 0, 0] },
          label: { show: true, position: 'top', fontSize: 12, color: '#262626' },
          data: courses.map((c, i) => ({
            value: c.totalStudents ?? 0,
            courseName: fullNames[i],
            completionRate: c.averageCompletionRate ?? 0,
            correctRate: c.averageCorrectRate ?? 0,
          })),
        },
      ],
    });

    this.studentPieChart?.setOption({
      tooltip: { trigger: 'item', formatter: '{b}: {c} 人 ({d}%)' },
      legend: {
        type: 'scroll',
        bottom: 0,
        textStyle: { fontSize: 13, color: '#262626' },
        itemWidth: 14,
        itemHeight: 10,
      },
      series: [{
        type: 'pie',
        radius: ['44%', '70%'],
        center: ['50%', '44%'],
        avoidLabelOverlap: true,
        // 单色系分级配色：明度差区分扇形，避免多色花哨，保持专业统一
        color: this.pieColors(courses.length),
        itemStyle: { borderRadius: 6 },
        label: { show: true, position: 'inside', formatter: '{d}%', fontSize: 11, fontWeight: 600, color: '#fff' },
        labelLayout: { hideOverlap: true },
        data: courses.map(c => ({ name: this.truncate(c.courseName ?? '', 12), value: c.totalStudents ?? 0 })),
      }],
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
   * 与柱状图统一色系：都基于品牌蓝 hsl(211,100%,54%)，
   * 用明度差区分扇形，明度控制在 38%~58%，保证白色百分比文字可读。
   */
  private pieColors(count: number): string[] {
    if (count <= 0) return [];
    if (count === 1) return ['hsl(211, 88%, 48%)'];
    const colors: string[] = [];
    for (let i = 0; i < count; i++) {
      const t = i / (count - 1);
      const l = Math.round(38 + (58 - 38) * t);
      colors.push(`hsl(211, 88%, ${l}%)`);
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
