import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { ChapterService } from '../../proxy/courses/chapter.service';
import type { ChapterDto } from '../../proxy/courses/dtos/models';
import { CourseService } from '../../proxy/courses/course.service';
import { CourseContextService } from '../course-context.service';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';
import {
  CourseIdeologySettingDto,
  IDEOLOGY_STATUS_LABEL,
  IDEOLOGY_USAGE_LABEL,
  IdeologyCaseDto,
  IdeologyCoverageDto,
  IdeologyService,
} from './ideology.service';

interface CourseOption {
  id: string;
  title?: string;
}

/**
 * 思政覆盖分析与报告（合并一页）。
 *
 * <p>排版思路：先给"整体覆盖"（4 个指标 + 环形覆盖率 + 元素分布条形图），
 * 再按需展开单个章节的细节（章节可下拉选择，**不一次性铺开所有章节**），
 * 最后是课程级设置与报告导出（xlsx）。图表全部用 SVG / CSS 自绘，不引入图表库。</p>
 */
@Component({
  selector: 'app-ideology-coverage',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzSelectModule,
    NzSwitchModule,
    NzTagModule,
    NzIconModule,
    NzEmptyModule,
    NzTooltipModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ideology-coverage.component.html',
  styleUrls: ['./ideology-pages.scss'],
})
export class IdeologyCoverageComponent implements OnInit {
  private readonly service = inject(IdeologyService);
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly authFetch = inject(AuthFetchService);

  readonly statusLabel = IDEOLOGY_STATUS_LABEL;
  readonly usageLabel = IDEOLOGY_USAGE_LABEL;

  readonly courses = signal<CourseOption[]>([]);
  readonly courseId = signal<string | null>(null);
  readonly chapters = signal<ChapterDto[]>([]);
  readonly coverage = signal<IdeologyCoverageDto | null>(null);
  readonly setting = signal<CourseIdeologySettingDto | null>(null);
  readonly goal = signal('');
  readonly exporting = signal(false);
  readonly savingSetting = signal(false);

  /** 选中的章节（null = 只有课程通用案例）。 */
  readonly chapterId = signal<string | null>(null);
  readonly chapterCases = signal<IdeologyCaseDto[]>([]);
  readonly chapterCasesLoading = signal(false);

  // ── 整体覆盖指标 ──
  readonly overall = computed(() => {
    const data = this.coverage();
    if (!data) {
      return null;
    }

    // 只统计真实章节（排除「课程通用」这一行，它的 id 是 Guid.Empty）。
    const realChapters = data.chapters.filter(c => c.id !== '00000000-0000-0000-0000-000000000000');
    const coveredChapters = realChapters.filter(c => c.caseCount > 0).length;
    const coveredElements = data.elements.filter(e => e.caseCount > 0).length;

    return {
      chapterTotal: realChapters.length,
      chapterCovered: coveredChapters,
      chapterPercent: realChapters.length ? Math.round((coveredChapters * 1000) / realChapters.length) / 10 : 0,
      elementTotal: data.elements.length,
      elementCovered: coveredElements,
      elementPercent: data.elements.length ? Math.round((coveredElements * 1000) / data.elements.length) / 10 : 0,
      published: data.publishedCases,
      total: data.totalCases,
    };
  });

  /** 元素分布条形图的数据（按案例数降序，最多展示 8 个，其余折叠为"其它"）。 */
  readonly elementBars = computed(() => {
    const data = this.coverage();
    if (!data) return [];

    const sorted = [...data.elements].sort((a, b) => b.caseCount - a.caseCount);
    const max = Math.max(1, ...sorted.map(e => e.caseCount));
    return sorted.slice(0, 8).map(item => ({
      name: item.name,
      count: item.caseCount,
      percent: item.percent,
      width: Math.round((item.caseCount * 100) / max),
    }));
  });

  /** 环形图参数（两段：已覆盖 / 未覆盖），半径固定，描边用 stroke-dasharray。 */
  readonly donuts = computed(() => {
    const data = this.overall();
    const radius = 46;
    const circumference = 2 * Math.PI * radius;
    const build = (percent: number) => ({
      radius,
      circumference,
      dash: (Math.max(0, Math.min(100, percent)) * circumference) / 100,
      gap: circumference,
    });

    return {
      chapter: build(data?.chapterPercent ?? 0),
      element: build(data?.elementPercent ?? 0),
    };
  });

  ngOnInit(): void {
    const fromQuery = this.route.snapshot.queryParamMap.get('courseId');
    this.loadCourses(fromQuery || this.courseContext.currentCourseId());
  }

  private loadCourses(initial: string | null): void {
    this.courseService.getMyCourses({ maxResultCount: 200, skipCount: 0 }).subscribe({
      next: list => {
        this.courses.set((list.items || []).map(c => ({ id: c.id!, title: c.title })));
        const target =
          initial && this.courses().some(c => c.id === initial) ? initial : this.courses()[0]?.id ?? null;
        if (target) {
          this.selectCourse(target);
        }
      },
      error: () => this.courses.set([]),
    });
  }

  selectCourse(courseId: string): void {
    this.courseId.set(courseId);
    this.courseContext.setCourse(courseId);
    this.chapterId.set(null);
    this.chapterCases.set([]);

    this.chapterService.getChaptersByCourse(courseId).subscribe({
      next: list => this.chapters.set(list || []),
      error: () => this.chapters.set([]),
    });

    this.reload();
  }

  onCourseChange(courseId: string): void {
    this.selectCourse(courseId);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { courseId },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  reload(): void {
    const courseId = this.courseId();
    if (!courseId) return;

    this.service.getCoverage(courseId).subscribe({
      next: data => {
        this.coverage.set(data);
        // 默认选第一个"还没覆盖"的章节，直接指引老师去补
        if (!this.chapterId()) {
          const firstGap = data.chapters.find(
            c => c.id !== '00000000-0000-0000-0000-000000000000' && c.caseCount === 0,
          );
          const target = firstGap?.id ?? this.chapters()[0]?.id ?? null;
          if (target) {
            this.onChapterChange(target);
          }
        } else {
          this.onChapterChange(this.chapterId());
        }
      },
      error: () => this.message.error('读取覆盖分析失败'),
    });

    this.service.getSetting(courseId).subscribe({
      next: data => {
        this.setting.set(data);
        this.goal.set(data.ideologyGoal ?? '');
      },
      error: () => this.setting.set(null),
    });
  }

  /** 选中章节 → 只加载这一章的案例（不再一次性铺开所有章节）。 */
  onChapterChange(chapterId: string | null): void {
    this.chapterId.set(chapterId);
    this.chapterCases.set([]);

    const courseId = this.courseId();
    if (!courseId) return;

    this.chapterCasesLoading.set(true);
    this.service
      .getCases({ courseId, chapterId, maxResultCount: 50, skipCount: 0 })
      .subscribe({
        next: result => {
          this.chapterCases.set(result.items || []);
          this.chapterCasesLoading.set(false);
        },
        error: () => {
          this.chapterCasesLoading.set(false);
          this.chapterCases.set([]);
        },
      });
  }

  chapterCoverage(chapterId: string | null): { count: number; percent: number } {
    const data = this.coverage();
    if (!data || !chapterId) return { count: 0, percent: 0 };
    const row = data.chapters.find(c => c.id === chapterId);
    return { count: row?.caseCount ?? 0, percent: row?.percent ?? 0 };
  }

  chapterName(chapterId: string | null): string {
    if (!chapterId) return '课程通用（未绑章节）';
    return this.chapters().find(c => c.id === chapterId)?.title ?? '章节已删除';
  }

  caseTextPreview(item: IdeologyCaseDto): string {
    return IdeologyService.caseText(item.contentJson ?? null, 120);
  }

  /** 导出 xlsx：`AuthFetchService.fetch` 拿二进制后用 blob 触发下载（下载接口要带 Bearer 令牌）。 */
  async exportReport(): Promise<void> {
    const courseId = this.courseId();
    if (!courseId) return;

    this.exporting.set(true);
    try {
      const response = await this.authFetch.fetch(this.service.getCoverageExportUrl(courseId), { method: 'POST' });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const blob = await response.blob();
      const fileName = this.resolveFileName(response) ?? `课程思政覆盖分析_${courseId.slice(0, 8)}.xlsx`;
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);

      this.message.success('已开始下载报告');
    } catch {
      this.message.error('导出失败');
    } finally {
      this.exporting.set(false);
    }
  }

  private resolveFileName(response: Response): string | null {
    const disposition = response.headers.get('content-disposition');
    if (!disposition) return null;
    const match = /filename\*?=(?:UTF-8'')?"?([^;"]+)"?/i.exec(disposition);
    return match ? decodeURIComponent(match[1].trim()) : null;
  }

  toggleEnabled(value: boolean): void {
    const current = this.setting();
    if (!current) return;
    this.setting.set({ ...current, isEnabled: value });
  }

  saveSetting(): void {
    const courseId = this.courseId();
    const current = this.setting();
    if (!courseId || !current) return;

    this.savingSetting.set(true);
    this.service
      .saveSetting({
        courseId,
        isEnabled: current.isEnabled,
        ideologyGoal: this.goal()?.trim() || null,
        note: current.note ?? null,
      })
      .subscribe({
        next: data => {
          this.savingSetting.set(false);
          this.setting.set(data);
          this.message.success('已保存课程思政设置');
        },
        error: () => {
          this.savingSetting.set(false);
          this.message.error('保存失败');
        },
      });
  }

  goCases(): void {
    const chapterId = this.chapterId();
    this.router.navigate(['/ideology/cases'], {
      queryParams: { courseId: this.courseId(), ...(chapterId ? { chapterId } : {}) },
    });
  }

  goGraph(): void {
    this.router.navigate(['/ideology/graph'], { queryParams: { courseId: this.courseId() } });
  }
}
