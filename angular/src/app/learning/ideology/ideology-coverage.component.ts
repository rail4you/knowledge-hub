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
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { ChapterService } from '../../proxy/courses/chapter.service';
import type { ChapterDto } from '../../proxy/courses/dtos/models';
import { CourseService } from '../../proxy/courses/course.service';
import { CourseContextService } from '../course-context.service';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';
import {
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

/** 环形图参数：半径固定，进度用 stroke-dasharray 表达。 */
interface DonutGeometry {
  radius: number;
  circumference: number;
  dash: number;
  percent: number;
}

/**
 * 思政覆盖分析与报告（合并一页）。
 *
 * <p>排版：指标卡 → ① 覆盖率环形图（整行）→ ② 元素分布条形图（整行）→ 章节详情（可按章筛选）。
 * 两张图表各占一行、互不挤压；图表全部 SVG / CSS 自绘，不引入图表库。</p>
 *
 * <p>课程级设置（启用开关 / 课程思政总目标）不在这里，放到了「思政案例库」页的「课程设置」弹窗，
 * 保持这一页只讲数据与报告。</p>
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
  readonly exporting = signal(false);

  /** 选中的章节（null = 未选）。 */
  readonly chapterId = signal<string | null>(null);
  readonly chapterCases = signal<IdeologyCaseDto[]>([]);
  readonly chapterCasesLoading = signal(false);

  /** 空 Guid：后端用 Guid.Empty 表示「课程通用（未绑章节）」那一行。 */
  private readonly generalRowId = '00000000-0000-0000-0000-000000000000';

  readonly overall = computed(() => {
    const data = this.coverage();
    if (!data) {
      return null;
    }

    const realChapters = data.chapters.filter(c => c.id !== this.generalRowId);
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
      draft: data.draftCases,
      archived: data.archivedCases,
      aiGenerated: data.aiGeneratedCases,
      generalCount: data.chapters.find(c => c.id === this.generalRowId)?.caseCount ?? 0,
    };
  });

  readonly chapterDonut = computed<DonutGeometry>(() => this.buildDonut(this.overall()?.chapterPercent ?? 0));
  readonly elementDonut = computed<DonutGeometry>(() => this.buildDonut(this.overall()?.elementPercent ?? 0));

  /** 元素分布：按案例数倒序，柱长按最大值归一化。 */
  readonly elementBars = computed(() => {
    const data = this.coverage();
    if (!data) return [];

    const sorted = [...data.elements].sort((a, b) => b.caseCount - a.caseCount);
    const max = Math.max(1, ...sorted.map(e => e.caseCount));
    return sorted.map(item => ({
      name: item.name,
      count: item.caseCount,
      percent: item.percent,
      width: Math.round((item.caseCount * 100) / max),
    }));
  });

  /** 章节覆盖榜：有案例的排在前面，最多展示 8 章（其余在章节详情里按需看）。 */
  readonly topChapters = computed(() => {
    const data = this.coverage();
    if (!data) return [];

    const real = data.chapters.filter(c => c.id !== this.generalRowId);
    const max = Math.max(1, ...real.map(c => c.caseCount));
    return [...real]
      .filter(c => c.caseCount > 0)
      .sort((a, b) => b.caseCount - a.caseCount)
      .slice(0, 8)
      .map(item => ({
        id: item.id,
        name: item.name,
        count: item.caseCount,
        width: Math.round((item.caseCount * 100) / max),
      }));
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

        if (!this.chapterId()) {
          // 默认落在第一个「还没覆盖」的章节，直接指引老师去补
          const firstGap = data.chapters.find(c => c.id !== this.generalRowId && c.caseCount === 0);
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
  }

  /** 选中章节 → 只加载这一章的案例。 */
  onChapterChange(chapterId: string | null): void {
    this.chapterId.set(chapterId);
    this.chapterCases.set([]);

    const courseId = this.courseId();
    if (!courseId) return;

    this.chapterCasesLoading.set(true);
    this.service.getCases({ courseId, chapterId, maxResultCount: 50, skipCount: 0 }).subscribe({
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

  pickChapter(chapterId: string): void {
    this.onChapterChange(chapterId);
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

  /** 案例正文摘要（列表用）。 */
  caseTextPreview(item: IdeologyCaseDto): string {
    return IdeologyService.caseText(item.contentJson ?? null, 140);
  }

  /** 导出 xlsx：`AuthFetchService.fetch` 拿二进制后用 blob 触发下载（接口要带 Bearer 令牌）。 */
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

  goCases(): void {
    const chapterId = this.chapterId();
    this.router.navigate(['/ideology/cases'], {
      queryParams: { courseId: this.courseId(), ...(chapterId ? { chapterId } : {}) },
    });
  }

  goGraph(): void {
    this.router.navigate(['/ideology/graph'], { queryParams: { courseId: this.courseId() } });
  }

  private buildDonut(percent: number): DonutGeometry {
    const radius = 54;
    const circumference = 2 * Math.PI * radius;
    const clamped = Math.max(0, Math.min(100, percent));
    return {
      radius,
      circumference,
      dash: (clamped * circumference) / 100,
      percent: clamped,
    };
  }
}
