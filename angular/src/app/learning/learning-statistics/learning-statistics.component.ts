import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import { CourseService } from '../../proxy/courses/course.service';
import { StudentLearningStatisticsDto, CourseLearningOverviewDto, GetLearningStatisticsInput } from '../../proxy/learning/dtos/models';

interface CourseItem {
  id: string;
  title: string;
}

@Component({
  selector: 'app-learning-statistics',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzTableModule,
    NzSelectModule,
    NzDatePickerModule,
    NzIconModule,
    NzSpinModule,
    NzTagModule,
    NzStatisticModule,
    NzGridModule,
    NzTooltipModule
  ],
  templateUrl: './learning-statistics.component.html',
  styleUrls: ['./learning-statistics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LearningStatisticsComponent implements OnInit {
  private readonly recordService = inject(StudentExerciseRecordService);
  private readonly courseService = inject(CourseService);
  private readonly message = inject(NzMessageService);

  loading = signal(false);
  exporting = signal(false);
  courses = signal<CourseItem[]>([]);
  selectedCourseId: string | null = null;
  selectedChapterId: string | null = null;
  dateRange: Date[] | null = null;

  overview = signal<CourseLearningOverviewDto | null>(null);
  statistics = signal<StudentLearningStatisticsDto[]>([]);
  totalCount = signal(0);
  pageIndex = signal(1);
  pageSize = signal(10);

  ngOnInit() {
    this.loadCourses();
  }

  loadCourses() {
    this.courseService.getList({ skipCount: 0, maxResultCount: 100 }).subscribe({
      next: (result) => {
        this.courses.set((result.items ?? []).map(c => ({ id: c.id!, title: c.title ?? '' })));
      },
      error: () => {
        this.message.error('加载课程列表失败');
      }
    });
  }

  onCourseChange() {
    this.pageIndex.set(1);
    this.loadOverview();
    this.loadStatistics();
  }

  loadOverview() {
    if (!this.selectedCourseId) return;

    this.recordService.getCourseLearningOverview({ courseId: this.selectedCourseId }).subscribe({
      next: (data) => {
        this.overview.set(data);
      },
      error: () => {
        this.message.error('加载概览失败');
      }
    });
  }

  loadStatistics() {
    if (!this.selectedCourseId) return;

    this.loading.set(true);
    const input: GetLearningStatisticsInput = {
      courseId: this.selectedCourseId,
      chapterId: this.selectedChapterId,
      startTime: this.dateRange?.[0]?.toISOString(),
      endTime: this.dateRange?.[1]?.toISOString(),
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize()
    };

    this.recordService.getLearningStatistics(input).subscribe({
      next: (result) => {
        this.statistics.set(result.items ?? []);
        this.totalCount.set(result.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => {
        this.message.error('加载统计数据失败');
        this.loading.set(false);
      }
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

  onDateRangeChange() {
    this.pageIndex.set(1);
    this.loadStatistics();
  }

  exportExcel() {
    if (!this.selectedCourseId) {
      this.message.warning('请先选择课程');
      return;
    }

    this.exporting.set(true);
    const input: GetLearningStatisticsInput = {
      courseId: this.selectedCourseId,
      chapterId: this.selectedChapterId,
      startTime: this.dateRange?.[0]?.toISOString(),
      endTime: this.dateRange?.[1]?.toISOString(),
      skipCount: 0,
      maxResultCount: 10000
    };

    this.recordService.exportLearningStatistics(input).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `学习统计_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.exporting.set(false);
        this.message.success('导出成功');
      },
      error: () => {
        this.message.error('导出失败');
        this.exporting.set(false);
      }
    });
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
