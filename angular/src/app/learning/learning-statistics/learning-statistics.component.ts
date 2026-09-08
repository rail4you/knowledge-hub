import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzMessageService } from 'ng-zorro-antd/message';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import {
  StudentLearningStatisticsDto,
  CourseLearningOverviewDto,
  GetLearningStatisticsInput,
} from '../../proxy/learning/dtos/models';

@Component({
  selector: 'app-learning-statistics',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzTableModule,
    NzDatePickerModule,
    NzIconModule,
    NzSpinModule,
    NzTagModule,
    NzTooltipModule,
    NzEmptyModule,
    NzInputModule,
    NzModalModule,
    NzTabsModule,
  ],
  templateUrl: './learning-statistics.component.html',
  styleUrls: ['./learning-statistics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LearningStatisticsComponent implements OnInit {
  private readonly recordService = inject(StudentExerciseRecordService);
  private readonly courseService = inject(CourseService);
  private readonly message = inject(NzMessageService);

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

  // ===== 右侧导出（时间范围仅在导出弹窗中确认） =====
  selectedChapterId: string | null = null;
  exporting = signal(false);
  exportModalVisible = signal(false);
  exportDateRange: Date[] | null = null;

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

  readonly chapterProgressList = computed(() => this.overview()?.chapterProgress ?? []);
  readonly chapterTotal = computed(() => this.chapterProgressList().length);
  readonly pagedChapterProgress = computed(() => {
    const all = this.chapterProgressList();
    const start = (this.chapterPageIndex() - 1) * this.chapterPageSize();
    return all.slice(start, start + this.chapterPageSize());
  });

  ngOnInit() {
    this.loadCourses();
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

  openExportModal() {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先从左侧选择课程');
      return;
    }
    this.exportDateRange = null;
    this.exportModalVisible.set(true);
  }

  closeExportModal() {
    if (this.exporting()) return;
    this.exportModalVisible.set(false);
  }

  confirmExport() {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先从左侧选择课程');
      return;
    }

    this.exporting.set(true);
    const input: GetLearningStatisticsInput = {
      courseId,
      chapterId: this.selectedChapterId,
      startTime: this.exportDateRange?.[0]?.toISOString(),
      endTime: this.exportDateRange?.[1]?.toISOString(),
      skipCount: 0,
      maxResultCount: 10000,
    };

    this.recordService.exportLearningStatistics(input).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `学习统计_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.exporting.set(false);
        this.exportModalVisible.set(false);
        this.message.success('导出成功');
      },
      error: () => {
        this.message.error('导出失败');
        this.exporting.set(false);
      },
    });
  }

  // 保留旧方法名兼容模板误调用，统一走弹窗
  exportExcel() {
    this.openExportModal();
  }

  getScoreColor(rate: number): string {
    if (rate >= 80) return '#52c41a';
    if (rate >= 60) return '#faad14';
    return '#ff4d4f';
  }

  formatTimeSpent(timeStr: string | undefined): string {
    if (!timeStr) return '-';
    const duration = typeof timeStr === 'string' ? timeStr : '00:00:00';
    return duration;
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
