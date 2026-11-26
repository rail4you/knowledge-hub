import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { LearningProfileService } from '../../proxy/learning/learning-profile.service';
import type { CourseInsightDto, ProfileReportDto } from '../../proxy/learning/dtos/models';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { MyCoursesService } from '../shared/my-courses.service';
import { CourseContextService } from '../course-context.service';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';

/**
 * 数据洞察（教学数据画像 · 页 2）。
 *
 * ⚠️ 这里的"洞察"是**规则式结论**（阈值 + 趋势对比），不是大模型生成的：
 * kg-edu 的 insights/engine 走 LLM 问数，但那需要模型与数据集注册；这一版先把
 * "能自动说清楚的结论"用规则产出，保证每次结果可解释、可复现。
 *
 * 报告模板 → 生成结构化报告（页面预览）+ 导出 xlsx。
 */
@Component({
  selector: 'app-insight',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTagModule,
    NzTableModule,
  ],
  templateUrl: './insight.component.html',
  styleUrls: ['./insight.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InsightComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(LearningProfileService);
  private readonly authFetch = inject(AuthFetchService);
  private readonly message = inject(NzMessageService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly days = signal(30);
  readonly template = signal('monthly');

  readonly insight = signal<CourseInsightDto | null>(null);
  readonly report = signal<ProfileReportDto | null>(null);
  readonly loading = signal(false);
  readonly building = signal(false);

  readonly templates = [
    { value: 'monthly', label: '课程学习月报' },
    { value: 'weekly', label: '课程学习周报' },
    { value: 'student', label: '学生学习情况报告' },
  ];

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
      this.insight.set(null);
      this.report.set(null);
      return;
    }
    this.courseContext.setCourse(courseId);
    this.loadInsight();
  }

  onDaysChange(days: number): void {
    this.days.set(days);
    this.loadInsight();
  }

  loadInsight(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.service.getCourseInsight(courseId, this.days()).subscribe({
      next: data => {
        this.insight.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.insight.set(null);
        this.loading.set(false);
        this.message.error('洞察加载失败');
      },
    });
  }

  buildReport(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.building.set(true);
    this.service.buildReport(courseId, this.template(), this.days()).subscribe({
      next: data => {
        this.report.set(data);
        this.building.set(false);
      },
      error: () => {
        this.building.set(false);
        this.message.error('报告生成失败');
      },
    });
  }

  /** 导出 xlsx：IRemoteStreamContent 必须用原生 fetch（不带 Accept: application/json） */
  exportReport(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    const url = `/api/app/learning-profile/export-report/${courseId}?template=${this.template()}&days=${this.days()}`;
    this.authFetch
      .fetch(url, { method: 'POST' })
      .then(async response => {
        if (!response.ok) {
          this.message.error('导出失败');
          return;
        }
        const blob = await response.blob();
        const objectUrl = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = `${this.templateLabel()}_${new Date().toISOString().slice(0, 10)}.xlsx`;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(objectUrl);
        this.message.success('已开始下载');
      })
      .catch(() => this.message.error('导出失败（网络或服务异常）'));
  }

  templateLabel(): string {
    return this.templates.find(t => t.value === this.template())?.label ?? '学习报告';
  }

  levelColor(level?: string): string {
    if (level === 'danger') return 'error';
    if (level === 'warning') return 'warning';
    return 'processing';
  }
}
