import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { Subject, Subscription, takeUntil , take } from 'rxjs';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import type { ChapterDto, CourseDto } from '../../proxy/courses/dtos/models';
import {
  AiGenerationTaskDto,
  AiTaskService,
  AiTaskStatus,
  AiTaskType,
} from '../../ai/services/ai-task.service';
import { AiTaskNotificationService } from '../../ai/services/ai-task-notification.service';
import {
  CourseCaseContent,
  CourseCaseDto,
  CourseCaseKeyIssue,
  CourseCaseService,
  CourseCaseSolution,
  CourseCaseSource,
  emptyCourseCaseContent,
} from './course-case.service';

interface ChapterOption {
  id: string;
  title: string;
  path: string;
  level: number;
}

interface IssueFormRow {
  title: string;
  description: string;
  impact: string;
  severity: string;
}

interface SolutionFormRow {
  title: string;
  description: string;
  stepsText: string;
  expectedOutcome: string;
}

const SEVERITIES = ['高', '中', '低'];

@Component({
  selector: 'app-course-case-library',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzPopconfirmModule,
    NzProgressModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
  ],
  templateUrl: './course-case.component.html',
  styleUrls: ['./course-case.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseCaseComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
  private readonly route = inject(ActivatedRoute);
  private readonly chapterService = inject(ChapterService);
  private readonly caseService = inject(CourseCaseService);
  private readonly aiTaskService = inject(AiTaskService);
  private readonly aiTaskNotifications = inject(AiTaskNotificationService);
  private readonly message = inject(NzMessageService);
  private readonly destroy$ = new Subject<void>();

  readonly severities = SEVERITIES;

  // ── 列表 ──
  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly chapters = signal<ChapterOption[]>([]);
  readonly selectedChapterId = signal<string | null>(null);
  readonly selectedSource = signal<CourseCaseSource | null>(null);
  readonly keyword = signal('');
  readonly cases = signal<CourseCaseDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  readonly selectedCourse = computed(
    () => this.courses().find((c) => c.id === this.selectedCourseId()) ?? null,
  );

  readonly hasCourse = computed(() => !!this.selectedCourseId());

  /** 当前筛选下的来源分布（页头统计，便于教师一眼看到 AI 产出占比） */
  readonly sourceStats = computed(() => {
    const list = this.cases();
    return {
      ai: list.filter((x) => x.source === CourseCaseSource.Ai).length,
      manual: list.filter((x) => x.source === CourseCaseSource.Manual).length,
      imported: list.filter((x) => x.source === CourseCaseSource.Import).length,
    };
  });

  // ── 新建 / 编辑表单 ──
  readonly editorVisible = signal(false);
  readonly editorSaving = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formTitle = signal('');
  readonly formSummary = signal('');
  readonly formChapterId = signal<string | null>(null);
  readonly formIndustry = signal('');
  readonly formTimeframe = signal('');
  readonly formContext = signal('');
  readonly formStakeholders = signal('');
  readonly formIssues = signal<IssueFormRow[]>([]);
  readonly formSolutions = signal<SolutionFormRow[]>([]);
  readonly formInsights = signal('');
  readonly formRecommendations = signal('');

  readonly isEditing = computed(() => !!this.editingId());

  // ── 详情 ──
  readonly detailVisible = signal(false);
  readonly detail = signal<CourseCaseDto | null>(null);

  // ── 导入 ──
  readonly importVisible = signal(false);
  readonly importing = signal(false);
  readonly importFileName = signal('');
  private importFile: File | null = null;

  // ── AI 生成 ──
  readonly aiVisible = signal(false);
  readonly aiChapterId = signal<string | null>(null);
  readonly aiTopicHint = signal('');
  readonly aiFocusArea = signal('');
  readonly aiCustomPrompt = signal('');
  readonly aiGenerating = signal(false);
  readonly aiProgress = signal(0);
  readonly aiMessage = signal('');
  private aiPollSub: Subscription | null = null;
  private aiTaskId: string | null = null;

  ngOnInit(): void {
    this.loadCourses();
    // 支持从「课程工作台」带 courseId 直达：进入即选中该课程，免去再次选课
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      const courseId = params.get('courseId');
      if (courseId) {
        this.onCourseChange(courseId);
      }
    });
  }

  ngOnDestroy(): void {
    this.aiPollSub?.unsubscribe();
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── 数据加载 ──
  private loadCourses(): void {
    this.courseService
      .getList({ skipCount: 0, maxResultCount: 200 } as any)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => this.courses.set(result.items || []),
        error: () => this.message.error('加载课程列表失败'),
      });
  }

  onCourseChange(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    this.selectedChapterId.set(null);
    this.chapters.set([]);
    this.pageIndex.set(1);
    this.cases.set([]);
    this.totalCount.set(0);

    if (!courseId) {
      return;
    }
    this.loadChapters(courseId);
    this.loadCases();
  }

  private loadChapters(courseId: string): void {
    this.chapterService
      .getChapterTree(courseId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (tree) => this.chapters.set(this.flattenChapters(tree || [])),
        error: () => this.message.error('加载章节失败'),
      });
  }

  private flattenChapters(tree: ChapterDto[]): ChapterOption[] {
    const out: ChapterOption[] = [];
    const walk = (nodes: ChapterDto[], ancestors: string[], level: number) => {
      for (const node of nodes || []) {
        const title = node.title || '未命名章节';
        const path = [...ancestors, title].join(' / ');
        if (node.id) {
          out.push({ id: node.id, title, path, level });
        }
        if (node.children && node.children.length > 0) {
          walk(node.children, [...ancestors, title], level + 1);
        }
      }
    };
    walk(tree, [], 0);
    return out;
  }

  loadCases(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      return;
    }

    this.loading.set(true);
    this.caseService
      .getList({
        courseId,
        chapterId: this.selectedChapterId() || undefined,
        source: this.selectedSource() ?? undefined,
        filter: this.keyword().trim() || undefined,
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.cases.set(result.items || []);
          this.totalCount.set(result.totalCount || 0);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载案例列表失败');
        },
      });
  }

  onChapterFilterChange(chapterId: string | null): void {
    this.selectedChapterId.set(chapterId);
    this.pageIndex.set(1);
    this.loadCases();
  }

  onSourceFilterChange(source: CourseCaseSource | null): void {
    this.selectedSource.set(source);
    this.pageIndex.set(1);
    this.loadCases();
  }

  onKeywordChange(value: string): void {
    this.keyword.set(value);
  }

  onKeywordSearch(): void {
    this.pageIndex.set(1);
    this.loadCases();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.loadCases();
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
    this.loadCases();
  }

  // ── 来源标签 ──
  sourceLabel(source: CourseCaseSource): string {
    switch (source) {
      case CourseCaseSource.Ai:
        return 'AI生成';
      case CourseCaseSource.Import:
        return '批量导入';
      default:
        return '手动录入';
    }
  }

  sourceColor(source: CourseCaseSource): string {
    switch (source) {
      case CourseCaseSource.Ai:
        return 'purple';
      case CourseCaseSource.Import:
        return 'blue';
      default:
        return 'default';
    }
  }

  severityColor(severity: string): string {
    if (severity === '高') return 'red';
    if (severity === '低') return 'green';
    return 'orange';
  }

  // ── 新建 / 编辑 ──
  openCreate(): void {
    if (!this.hasCourse()) {
      this.message.warning('请先选择课程');
      return;
    }
    this.editingId.set(null);
    this.formTitle.set('');
    this.formSummary.set('');
    this.formChapterId.set(this.selectedChapterId());
    this.formIndustry.set('');
    this.formTimeframe.set('');
    this.formContext.set('');
    this.formStakeholders.set('');
    this.formIssues.set([]);
    this.formSolutions.set([]);
    this.formInsights.set('');
    this.formRecommendations.set('');
    this.editorVisible.set(true);
  }

  openEdit(item: CourseCaseDto): void {
    this.caseService
      .get(item.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (detail) => {
          const content = detail.content ?? emptyCourseCaseContent(detail.title);
          this.editingId.set(detail.id);
          this.formTitle.set(detail.title);
          this.formSummary.set(detail.summary || content.summary || '');
          this.formChapterId.set(detail.chapterId || null);
          this.formIndustry.set(content.background?.industry || '');
          this.formTimeframe.set(content.background?.timeframe || '');
          this.formContext.set(content.background?.context || '');
          this.formStakeholders.set((content.background?.stakeholders || []).join('\n'));
          this.formIssues.set(
            (content.keyIssues || []).map((x) => ({
              title: x.title || '',
              description: x.description || '',
              impact: x.impact || '',
              severity: x.severity || '中',
            })),
          );
          this.formSolutions.set(
            (content.solutions || []).map((x) => ({
              title: x.title || '',
              description: x.description || '',
              stepsText: (x.steps || []).join('\n'),
              expectedOutcome: x.expectedOutcome || '',
            })),
          );
          this.formInsights.set((content.keyInsights || []).join('\n'));
          this.formRecommendations.set((content.recommendations || []).join('\n'));
          this.editorVisible.set(true);
        },
        error: () => this.message.error('加载案例详情失败'),
      });
  }

  closeEditor(): void {
    if (this.editorSaving()) return;
    this.editorVisible.set(false);
  }

  addIssue(): void {
    this.formIssues.set([
      ...this.formIssues(),
      { title: '', description: '', impact: '', severity: '中' },
    ]);
  }

  removeIssue(index: number): void {
    this.formIssues.set(this.formIssues().filter((_, i) => i !== index));
  }

  addSolution(): void {
    this.formSolutions.set([
      ...this.formSolutions(),
      { title: '', description: '', stepsText: '', expectedOutcome: '' },
    ]);
  }

  removeSolution(index: number): void {
    this.formSolutions.set(this.formSolutions().filter((_, i) => i !== index));
  }

  save(): void {
    const courseId = this.selectedCourseId();
    const title = this.formTitle().trim();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    if (!title) {
      this.message.warning('请填写案例标题');
      return;
    }

    const content = this.buildContent(title);
    const chapterId = this.formChapterId();
    const chapterTitle = this.chapters().find((c) => c.id === chapterId)?.title ?? null;

    this.editorSaving.set(true);
    const editingId = this.editingId();
    const request = editingId
      ? this.caseService.update(editingId, {
          title,
          summary: content.summary,
          chapterId,
          chapterTitle,
          content,
        })
      : this.caseService.create({
          courseId,
          chapterId,
          chapterTitle,
          title,
          summary: content.summary,
          content,
        });

    request.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.editorSaving.set(false);
        this.editorVisible.set(false);
        this.message.success(editingId ? '案例已更新' : '案例已创建');
        this.loadCases();
      },
      error: (err) => {
        this.editorSaving.set(false);
        this.message.error(this.errorText(err, '保存失败'));
      },
    });
  }

  private buildContent(title: string): CourseCaseContent {
    const issues: CourseCaseKeyIssue[] = this.formIssues()
      .map((row, index) => ({
        id: String(index + 1),
        title: row.title.trim(),
        description: row.description.trim(),
        impact: row.impact.trim(),
        severity: row.severity || '中',
      }))
      .filter((x) => x.title || x.description);

    const solutions: CourseCaseSolution[] = this.formSolutions()
      .map((row, index) => ({
        id: String(index + 1),
        title: row.title.trim(),
        description: row.description.trim(),
        steps: this.splitLines(row.stepsText),
        expectedOutcome: row.expectedOutcome.trim(),
      }))
      .filter((x) => x.title || x.description);

    return {
      title,
      summary: this.formSummary().trim(),
      background: {
        industry: this.formIndustry().trim(),
        timeframe: this.formTimeframe().trim(),
        context: this.formContext().trim(),
        stakeholders: this.splitLines(this.formStakeholders()),
      },
      keyIssues: issues,
      solutions,
      keyInsights: this.splitLines(this.formInsights()),
      recommendations: this.splitLines(this.formRecommendations()),
    };
  }

  private splitLines(value: string): string[] {
    return (value || '')
      .split('\n')
      .map((x) => x.trim().replace(/^[-·•*]\s*/, ''))
      .filter((x) => !!x);
  }

  // ── 详情 ──
  openDetail(item: CourseCaseDto): void {
    this.caseService
      .get(item.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (detail) => {
          this.detail.set(detail);
          this.detailVisible.set(true);
        },
        error: () => this.message.error('加载案例详情失败'),
      });
  }

  closeDetail(): void {
    this.detailVisible.set(false);
    this.detail.set(null);
  }

  // ── 删除 ──
  deleteCase(item: CourseCaseDto): void {
    this.caseService
      .delete(item.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.message.success('案例已删除');
          this.loadCases();
        },
        error: (err) => this.message.error(this.errorText(err, '删除失败')),
      });
  }

  // ── 导入 ──
  openImport(): void {
    if (!this.hasCourse()) {
      this.message.warning('请先选择课程');
      return;
    }
    this.importFile = null;
    this.importFileName.set('');
    this.importVisible.set(true);
  }

  closeImport(): void {
    if (this.importing()) return;
    this.importVisible.set(false);
  }

  onImportFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files && input.files.length > 0 ? input.files[0] : null;
    this.importFile = file;
    this.importFileName.set(file ? file.name : '');
  }

  submitImport(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    if (!this.importFile) {
      this.message.warning('请选择要导入的 Excel 文件');
      return;
    }

    this.importing.set(true);
    this.caseService
      .importFromExcel(courseId, this.importFile)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.importing.set(false);
          if (result.failCount === 0) {
            this.message.success(`导入成功，共 ${result.successCount} 条案例`);
            this.importVisible.set(false);
          } else {
            const detail = (result.errors || []).slice(0, 5).join('\n');
            this.message.warning(
              `导入完成：成功 ${result.successCount}，失败 ${result.failCount}\n${detail}`,
            );
          }
          this.pageIndex.set(1);
          this.loadCases();
        },
        error: (err) => {
          this.importing.set(false);
          this.message.error(this.errorText(err, '导入失败'));
        },
      });
  }

  // ── AI 生成 ──
  openAi(): void {
    if (!this.hasCourse()) {
      this.message.warning('请先选择课程');
      return;
    }
    if (this.chapters().length === 0) {
      this.message.warning('当前课程还没有章节，请先在「章节管理」中维护章节');
      return;
    }
    this.aiChapterId.set(this.selectedChapterId() || this.chapters()[0]?.id || null);
    this.aiTopicHint.set('');
    this.aiFocusArea.set('');
    this.aiCustomPrompt.set('');
    this.aiGenerating.set(false);
    this.aiProgress.set(0);
    this.aiMessage.set('');
    this.aiVisible.set(true);
  }

  closeAi(): void {
    if (this.aiGenerating()) {
      return;
    }
    this.aiVisible.set(false);
  }

  submitAi(): void {
    const course = this.selectedCourse();
    const chapterId = this.aiChapterId();
    if (!course || !chapterId) {
      this.message.warning('请选择课程章节');
      return;
    }

    const chapter = this.chapters().find((c) => c.id === chapterId);

    const input = {
      courseId: course.id,
      courseTitle: course.title,
      chapterId,
      chapterTitle: chapter?.title || '',
      topicHint: this.aiTopicHint().trim() || undefined,
      focusArea: this.aiFocusArea().trim() || undefined,
      customPrompt: this.aiCustomPrompt().trim() || undefined,
    };

    this.aiGenerating.set(true);
    this.aiProgress.set(0);
    this.aiMessage.set('任务已提交，正在后台生成…');

    this.aiTaskService
      .create({
        taskType: AiTaskType.CaseAnalysis,
        title: `${course.title} - ${chapter?.title || 'AI 案例'}`,
        resourceName: chapter?.title,
        inputJson: JSON.stringify(input),
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (task) => {
          this.aiTaskId = task.id;
          this.followAiTask(task.id);
        },
        error: (err) => {
          this.aiGenerating.set(false);
          this.message.error(this.errorText(err, '提交生成任务失败'));
        },
      });
  }

  cancelAi(): void {
    if (this.aiTaskId) {
      this.aiTaskService.cancel(this.aiTaskId).subscribe({ next: () => {}, error: () => {} });
    }
    this.aiPollSub?.unsubscribe();
    this.aiPollSub = null;
    this.aiGenerating.set(false);
    this.aiVisible.set(false);
    this.aiTaskId = null;
  }

  private followAiTask(taskId: string): void {
    this.aiPollSub?.unsubscribe();
    this.aiPollSub = this.aiTaskNotifications.pollTask(taskId).subscribe({
      next: (task: AiGenerationTaskDto) => {
        if (typeof task.progress === 'number') this.aiProgress.set(task.progress);
        if (task.progressMessage) this.aiMessage.set(task.progressMessage);
        if (task.status === AiTaskStatus.Completed) {
          this.aiPollSub?.unsubscribe();
          this.aiPollSub = null;
          this.aiGenerating.set(false);
          this.aiVisible.set(false);
          this.aiTaskId = null;
          this.message.success('AI 案例已生成并加入案例库');
          this.pageIndex.set(1);
          this.loadCases();
        } else if (
          task.status === AiTaskStatus.Failed ||
          task.status === AiTaskStatus.Cancelled
        ) {
          this.aiPollSub?.unsubscribe();
          this.aiPollSub = null;
          this.aiGenerating.set(false);
          this.aiTaskId = null;
          this.aiMessage.set(task.errorMessage || '生成失败');
          this.message.error(task.errorMessage || '生成失败，请重试');
        }
      },
      error: () => {
        this.aiPollSub?.unsubscribe();
        this.aiPollSub = null;
        this.aiGenerating.set(false);
        this.message.error('生成失败，请稍后重试');
      },
    });
  }

  private errorText(err: any, fallback: string): string {
    return err?.error?.error?.message || err?.error?.message || fallback;
  }
}
