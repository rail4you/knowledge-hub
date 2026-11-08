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
import { CourseContextService } from '../course-context.service';
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
import { NzStepsModule } from 'ng-zorro-antd/steps';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { Subject, Subscription, takeUntil , take } from 'rxjs';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { CourseResourceService } from '../../proxy/courses/course-resource.service';
import type { ChapterDto, CourseDto, CourseResourceDto } from '../../proxy/courses/dtos/models';
import { ChapterPickerComponent } from '../../shared/chapter-picker/chapter-picker.component';
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

/** AI 生成案例向导步骤：选择章节 → 预览确认 → 后台生成。 */
type AiStep = 'select' | 'preview' | 'generating';

/** 章节下的子章节（内容随本章一起进入生成提示词）。 */
interface AiSubChapter {
  title: string;
  /** 相对本章的路径（如「1.1 概述 / 1.1.1 起源」）。 */
  path: string;
  summary: string;
}

/** AI 生成案例待办的一个章节（勾选结果 → 预览确认 → 逐章生成）。 */
interface AiCaseChapterItem {
  id: string;
  order: number;
  /** 章节标题（预览时可修改）。 */
  title: string;
  /** 完整路径（预览时展示定位，悬停输入框可见）。 */
  path: string;
  /** 内容要点（案例生成依据，预览时可修改）。 */
  summary: string;
  /** 本章下的子章节（只显示数量，内容带入提示词）。 */
  children: AiSubChapter[];
  /** 本章案例主题提示。 */
  topicHint: string;
  /** 本章分析关注点。 */
  focusArea: string;
  /** 本章教师附加要求。 */
  customPrompt: string;
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
    NzStepsModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
    ChapterPickerComponent,
  ],
  templateUrl: './course-case.component.html',
  styleUrls: ['./course-case.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseCaseComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly chapterService = inject(ChapterService);
  private readonly courseResourceService = inject(CourseResourceService);
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

  // ── AI 生成（多章节：勾选章节 → 预览确认 → 后台逐章生成） ──
  readonly aiVisible = signal(false);
  /** 向导当前步骤。 */
  readonly aiStep = signal<AiStep>('select');
  /** 章节树（勾选用，含 children）。 */
  readonly aiChapterTree = signal<ChapterDto[]>([]);
  /** 勾选的章节 id（复选框多选）。 */
  readonly aiSelectedChapterIds = signal<string[]>([]);
  /** 预览确认的章节（有序，逐章可编辑）。 */
  readonly aiChapters = signal<AiCaseChapterItem[]>([]);
  /** 课程资源（参考资源）。 */
  readonly courseResources = signal<CourseResourceDto[]>([]);
  /** 统一设置（可一键应用到全部章节）：主题提示 / 关注点 / 附加要求 / 参考资源。 */
  readonly aiTopicHint = signal('');
  readonly aiFocusArea = signal('');
  readonly aiCustomPrompt = signal('');
  readonly aiResourceId = signal<string | null>(null);
  readonly aiGenerating = signal(false);
  readonly aiProgress = signal(0);
  readonly aiMessage = signal('');
  /** 当前正在生成的章节序号（从 1 开始）。 */
  readonly aiActiveChapter = signal(0);
  /** 已完成生成的章节序号集合。 */
  readonly aiDoneChapters = signal<Set<number>>(new Set());
  /** 本次任务的总章节数。 */
  readonly aiChapterTotal = signal(0);
  /** 已用时（秒）。 */
  readonly aiElapsed = signal(0);
  private aiPollSub: Subscription | null = null;
  private aiTaskId: string | null = null;
  private aiTimer: ReturnType<typeof setInterval> | null = null;
  private aiCloseTimer: ReturnType<typeof setTimeout> | null = null;
  private aiStartedAt = 0;
  /** 进行中的后台任务（切页回来时恢复进度面板）。 */
  readonly runningAiTask = signal<AiGenerationTaskDto | null>(null);

  ngOnInit(): void {
    this.loadCourses();
    // 支持从「课程工作台」带 courseId 直达：进入即选中该课程，免去再次选课
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      // 优先级：URL 上的 courseId > 全局「当前课程」
      const courseId = params.get('courseId') || this.courseContext.currentCourseId();
      if (courseId) {
        this.onCourseChange(courseId);
      }

      // ?taskId= 深链（AI 任务通知 / 任务中心跳转）：直接打开该任务的生成进度面板
      const taskId = params.get('taskId');
      if (taskId) {
        this.loadAiTaskPreview(taskId);
      }
    });

    // 切页回来：恢复我名下进行中的课程多章节案例任务（后台生成，进度不丢）
    this.resumeAiTask();
  }

  /** ?taskId= 深链：拉取任务并恢复进度面板（已完成则刷新列表）。 */
  private loadAiTaskPreview(taskId: string): void {
    this.aiTaskService
      .get(taskId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (task) => {
          if (task.taskType !== AiTaskType.CaseAnalysisCourseMulti) return;
          if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
            this.aiVisible.set(true);
            this.restoreAiTask(task);
          } else if (task.status === AiTaskStatus.Completed) {
            this.message.success('AI 案例已生成');
            this.pageIndex.set(1);
            this.loadCases();
          }
        },
        error: () => undefined,
      });
  }

  ngOnDestroy(): void {
    this.aiPollSub?.unsubscribe();
    this.stopAiTimer();
    this.clearAiPanelCloseTimer();
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
    this.courseContext.setCourse(courseId);
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

  // ── AI 生成（多章节：勾选章节 → 预览确认 → 后台逐章生成） ──
  openAi(): void {
    if (!this.hasCourse()) {
      this.message.warning('请先选择课程');
      return;
    }
    if (this.chapters().length === 0) {
      this.message.warning('当前课程还没有章节，请先在「章节管理」中维护章节');
      return;
    }
    // 已有任务在跑：直接看进度，不重复排队
    const running = this.runningAiTask();
    if (running) {
      this.message.info('已有案例生成任务在进行中，先查看进度');
      this.openAiProgress(running);
      return;
    }
    this.aiStep.set('select');
    this.aiSelectedChapterIds.set([]);
    this.aiChapters.set([]);
    this.aiTopicHint.set('');
    this.aiFocusArea.set('');
    this.aiCustomPrompt.set('');
    this.aiResourceId.set(null);
    this.aiGenerating.set(false);
    this.aiProgress.set(0);
    this.aiMessage.set('');
    this.aiActiveChapter.set(0);
    this.aiDoneChapters.set(new Set());
    this.aiChapterTotal.set(0);
    this.aiElapsed.set(0);
    this.aiVisible.set(true);
    this.loadAiChapterTree();
  }

  /** 拉取课程章节树（勾选用）+ 课程资源（参考资源）。 */
  private loadAiChapterTree(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.chapterService
      .getChapterTree(courseId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (tree) => this.aiChapterTree.set(tree || []),
        error: () => this.aiChapterTree.set([]),
      });
    this.courseResourceService
      .getByCourse(courseId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (list) => this.courseResources.set(list || []),
        error: () => this.courseResources.set([]),
      });
  }

  /** 章节树按顺序展开（含子章节内容，供提示词使用）。 */
  private readonly flatChapterTree = computed(() => {
    const out: {
      id: string;
      title: string;
      path: string;
      summary: string;
      children: AiSubChapter[];
    }[] = [];
    const collect = (node: ChapterDto, parentPath: string): AiSubChapter[] => {
      const children: AiSubChapter[] = [];
      for (const child of node.children || []) {
        const title = child.title || '(未命名章节)';
        const path = parentPath ? `${parentPath} / ${title}` : title;
        children.push({ title, path, summary: child.description || '' });
        children.push(...collect(child, path));
      }
      return children;
    };
    const walk = (nodes: ChapterDto[] | undefined, parentPath: string) => {
      for (const n of nodes || []) {
        const title = n.title || '(未命名章节)';
        const path = parentPath ? `${parentPath} / ${title}` : title;
        out.push({
          id: n.id,
          title,
          path,
          summary: n.description || '',
          children: collect(n, path),
        });
        walk(n.children, path);
      }
    };
    walk(this.aiChapterTree(), '');
    return out;
  });

  /** 勾选章节（复选框多选，父子不联动）。 */
  onAiSelectedIdsChange(ids: string[]): void {
    this.aiSelectedChapterIds.set(ids);
  }

  /** 选择章节 → 预览确认。 */
  aiNextToPreview(): void {
    const selected = new Set(this.aiSelectedChapterIds());
    const edited = new Map(this.aiChapters().map((c) => [c.id, c]));
    const list = this.flatChapterTree()
      .filter((c) => selected.has(c.id))
      .map((c, i) => {
        const prev = edited.get(c.id);
        return {
          id: c.id,
          order: i + 1,
          title: prev?.title ?? c.title,
          path: c.path,
          // 无章节描述时用章节标题兜底，避免生成时缺少本章信息
          summary: prev?.summary ?? (c.summary || c.title),
          children: c.children,
          topicHint: prev?.topicHint ?? this.aiTopicHint(),
          focusArea: prev?.focusArea ?? this.aiFocusArea(),
          customPrompt: prev?.customPrompt ?? this.aiCustomPrompt(),
        };
      });
    if (list.length === 0) {
      this.message.warning('请至少勾选一个章节');
      return;
    }
    this.aiChapters.set(list);
    this.aiStep.set('preview');
  }

  /** 预览确认 → 返回选择章节。 */
  aiBackToSelect(): void {
    this.aiStep.set('select');
  }

  updateAiChapterTitle(id: string, value: string): void {
    this.aiChapters.update((list) => list.map((c) => (c.id === id ? { ...c, title: value } : c)));
  }

  updateAiChapterSummary(id: string, value: string): void {
    this.aiChapters.update((list) => list.map((c) => (c.id === id ? { ...c, summary: value } : c)));
  }

  updateAiChapterTopicHint(id: string, value: string): void {
    this.aiChapters.update((list) => list.map((c) => (c.id === id ? { ...c, topicHint: value } : c)));
  }

  updateAiChapterFocusArea(id: string, value: string): void {
    this.aiChapters.update((list) => list.map((c) => (c.id === id ? { ...c, focusArea: value } : c)));
  }

  updateAiChapterCustomPrompt(id: string, value: string): void {
    this.aiChapters.update((list) => list.map((c) => (c.id === id ? { ...c, customPrompt: value } : c)));
  }

  /** 预览步骤：移除某个章节。 */
  removeAiChapter(id: string): void {
    this.aiChapters.update((list) =>
      list.filter((c) => c.id !== id).map((c, i) => ({ ...c, order: i + 1 })),
    );
    this.aiSelectedChapterIds.update((ids) => ids.filter((x) => x !== id));
  }

  /** 「统一设置」应用到全部章节：主题提示 / 关注点 / 附加要求。 */
  applyAiDefaultsToAll(): void {
    const topicHint = this.aiTopicHint();
    const focusArea = this.aiFocusArea();
    const customPrompt = this.aiCustomPrompt();
    this.aiChapters.update((list) =>
      list.map((c) => ({ ...c, topicHint, focusArea, customPrompt })),
    );
    this.message.success('已应用到全部章节');
  }

  /** 关闭弹窗（生成中也可关闭：任务在后台继续跑）。 */
  closeAi(): void {
    this.aiVisible.set(false);
  }

  /** 确认生成：提交后台任务（逐章生成，每章落一条案例）。 */
  submitAi(): void {
    const course = this.selectedCourse();
    if (!course) return;
    const chapters = this.aiChapters();
    if (chapters.length === 0) {
      this.message.warning('请至少勾选一个章节');
      return;
    }

    const input = {
      courseId: course.id,
      courseTitle: course.title,
      topicHint: this.aiTopicHint().trim() || undefined,
      focusArea: this.aiFocusArea().trim() || undefined,
      customPrompt: this.aiCustomPrompt().trim() || undefined,
      resourceId: this.aiResourceId() || undefined,
      chapters: chapters.map((c, i) => ({
        chapterId: c.id,
        order: i + 1,
        title: c.title.trim(),
        summary: c.summary?.trim() || undefined,
        topicHint: c.topicHint?.trim() || undefined,
        focusArea: c.focusArea?.trim() || undefined,
        customPrompt: c.customPrompt?.trim() || undefined,
        subChapters: c.children?.length
          ? c.children.map((sub) => ({
              title: sub.title,
              path: sub.path,
              summary: sub.summary?.trim() || undefined,
            }))
          : undefined,
      })),
    };

    this.aiGenerating.set(true);
    this.aiProgress.set(0);
    this.aiMessage.set('任务已提交，正在后台生成…');
    this.aiStep.set('generating');
    this.aiChapterTotal.set(chapters.length);
    this.aiActiveChapter.set(0);
    this.aiDoneChapters.set(new Set());
    this.aiElapsed.set(0);
    this.startAiTimer();

    this.aiTaskService
      .create({
        taskType: AiTaskType.CaseAnalysisCourseMulti,
        title: `${course.title} - ${chapters.length} 章案例`,
        resourceName: course.title,
        inputJson: JSON.stringify(input),
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (task) => {
          this.aiTaskId = task.id;
          this.runningAiTask.set(task);
          this.followAiTask(task.id);
        },
        error: (err) => {
          this.aiGenerating.set(false);
          this.stopAiTimer();
          this.aiStep.set('preview');
          this.message.error(this.errorText(err, '提交生成任务失败'));
        },
      });
  }

  /** 取消生成：取消后台任务，回到预览步骤。 */
  cancelAi(): void {
    if (this.aiTaskId) {
      this.aiTaskService.cancel(this.aiTaskId).subscribe({ next: () => {}, error: () => {} });
    }
    this.aiPollSub?.unsubscribe();
    this.aiPollSub = null;
    this.aiGenerating.set(false);
    this.stopAiTimer();
    this.runningAiTask.set(null);
    this.aiTaskId = null;
    this.aiStep.set('preview');
    this.message.info('已取消生成');
  }

  private followAiTask(taskId: string): void {
    this.aiPollSub?.unsubscribe();
    this.aiPollSub = this.aiTaskNotifications.pollTask(taskId).subscribe({
      next: (task: AiGenerationTaskDto) => {
        if (typeof task.progress === 'number') this.aiProgress.set(task.progress);
        if (task.progressMessage) this.aiMessage.set(task.progressMessage);
        this.applyAiChapterProgress(task.progressMessage);
        if (task.status === AiTaskStatus.Completed) {
          this.aiPollSub?.unsubscribe();
          this.aiPollSub = null;
          this.aiGenerating.set(false);
          this.stopAiTimer();
          this.aiProgress.set(100);
          // 全部章节完成：逐章标记完成（后端最终文案只保留“已完成”），随后自动关闭进度面板
          this.markAllAiChaptersDone();
          this.aiMessage.set(`全部 ${this.aiChapterTotal()} 章案例已生成`);
          this.runningAiTask.set(null);
          this.aiTaskId = null;
          this.message.success(`AI 案例已生成并加入案例库（${this.aiChapterTotal()} 章）`);
          this.pageIndex.set(1);
          this.loadCases();
          this.scheduleAiPanelClose();
        } else if (
          task.status === AiTaskStatus.Failed ||
          task.status === AiTaskStatus.Cancelled
        ) {
          this.aiPollSub?.unsubscribe();
          this.aiPollSub = null;
          this.aiGenerating.set(false);
          this.stopAiTimer();
          this.runningAiTask.set(null);
          this.aiTaskId = null;
          this.aiMessage.set(task.errorMessage || '生成失败');
          this.message.error(task.errorMessage || '生成失败，请重试');
        }
      },
      error: () => {
        this.aiPollSub?.unsubscribe();
        this.aiPollSub = null;
        this.aiGenerating.set(false);
        this.stopAiTimer();
        this.message.error('生成失败，请稍后重试');
      },
    });
  }

  /** 从进度文案解析章节进度（「第 X/Y 章」/「第 X/Y 章已完成」）。 */
  private applyAiChapterProgress(message?: string): void {
    if (!message) return;
    const doneMatch = /第\s*(\d+)\s*\/\s*(\d+)\s*章已完成/.exec(message);
    const match = doneMatch ?? /第\s*(\d+)\s*\/\s*(\d+)\s*章/.exec(message);
    if (!match) return;
    const current = Number(match[1]);
    const total = Number(match[2]);
    if (!Number.isFinite(current) || !Number.isFinite(total) || total <= 0) return;
    this.aiChapterTotal.set(total);

    if (doneMatch) {
      const done = new Set(this.aiDoneChapters());
      done.add(current);
      this.aiDoneChapters.set(done);
      let next = 0;
      for (let i = 1; i <= total; i++) {
        if (!done.has(i)) {
          next = i;
          break;
        }
      }
      this.aiActiveChapter.set(next);
    } else {
      this.aiActiveChapter.set(current);
    }
  }

  /** 章节状态（进度列表）：已完成 / 生成中 / 待生成。 */
  aiChapterState(order: number): 'done' | 'active' | 'pending' {
    if (this.aiDoneChapters().has(order)) return 'done';
    const active = this.aiActiveChapter();
    if (active > 0 && order === active) return 'active';
    return 'pending';
  }

  /** 全部章节是否已生成完成。 */
  readonly allAiChaptersDone = computed(() => {
    const total = this.aiChapterTotal();
    return total > 0 && this.aiDoneChapters().size >= total;
  });

  /** 把全部章节标记为已完成（任务完成时用）。 */
  private markAllAiChaptersDone(): void {
    const total = this.aiChapterTotal() || this.aiChapters().length;
    const done = new Set<number>();
    for (let i = 1; i <= total; i++) done.add(i);
    this.aiDoneChapters.set(done);
    this.aiActiveChapter.set(0);
  }

  /** 全部完成后自动关闭进度面板，回到案例列表。 */
  private scheduleAiPanelClose(): void {
    this.clearAiPanelCloseTimer();
    this.aiCloseTimer = setTimeout(() => {
      this.aiCloseTimer = null;
      this.aiVisible.set(false);
    }, 1500);
  }

  private clearAiPanelCloseTimer(): void {
    if (this.aiCloseTimer !== null) {
      clearTimeout(this.aiCloseTimer);
      this.aiCloseTimer = null;
    }
  }

  /** 已用时 MM:SS。 */
  readonly aiElapsedText = computed(() => {
    const s = this.aiElapsed();
    const m = Math.floor(s / 60);
    return `${String(m).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  });

  private startAiTimer(): void {
    this.stopAiTimer();
    this.aiStartedAt = Date.now();
    this.aiTimer = setInterval(() => {
      this.aiElapsed.set(Math.floor((Date.now() - this.aiStartedAt) / 1000));
    }, 1000);
  }

  private stopAiTimer(): void {
    if (this.aiTimer !== null) {
      clearInterval(this.aiTimer);
      this.aiTimer = null;
    }
  }

  /** 恢复进行中的任务（切页回来 / 通知深链）：还原章节与进度面板。 */
  private restoreAiTask(task: AiGenerationTaskDto): void {
    const chapters = this.parseAiChaptersFromInput(task.inputJson);
    this.aiChapters.set(chapters);
    this.aiChapterTotal.set(chapters.length);
    this.aiProgress.set(task.progress ?? 0);
    this.aiMessage.set(task.progressMessage || '后台生成中…');
    this.applyAiChapterProgress(task.progressMessage);
    this.aiTaskId = task.id;
    this.runningAiTask.set(task);
    this.aiGenerating.set(true);
    this.aiStep.set('generating');
    this.aiElapsed.set(0);
    this.startAiTimer();
    this.followAiTask(task.id);
  }

  /** 从任务 inputJson 还原章节列表。 */
  private parseAiChaptersFromInput(inputJson?: string): AiCaseChapterItem[] {
    if (!inputJson) return [];
    try {
      const input = JSON.parse(inputJson) as {
        topicHint?: string;
        focusArea?: string;
        customPrompt?: string;
        chapters?: {
          chapterId?: string;
          order?: number;
          title?: string;
          summary?: string;
          topicHint?: string;
          focusArea?: string;
          customPrompt?: string;
          subChapters?: { title?: string; path?: string; summary?: string }[];
        }[];
      };
      const list = Array.isArray(input.chapters) ? input.chapters : [];
      return list
        .filter((c) => c && typeof c.title === 'string' && c.title.trim().length > 0)
        .map((c, i) => ({
          id: c.chapterId || '',
          order: c.order || i + 1,
          title: c.title!.trim(),
          path: c.title!.trim(),
          summary: (c.summary || '').trim(),
          children: (c.subChapters || [])
            .filter((s) => s && typeof s.title === 'string' && s.title.trim().length > 0)
            .map((s) => ({
              title: s.title!.trim(),
              path: (s.path || s.title || '').trim(),
              summary: (s.summary || '').trim(),
            })),
          topicHint: c.topicHint ?? input.topicHint ?? '',
          focusArea: c.focusArea ?? input.focusArea ?? '',
          customPrompt: c.customPrompt ?? input.customPrompt ?? '',
        }));
    } catch {
      return [];
    }
  }

  /** 查询我名下进行中的课程多章节案例任务。 */
  private resumeAiTask(): void {
    this.aiTaskService
      .getList({ taskType: AiTaskType.CaseAnalysisCourseMulti, onlyMine: true, maxResultCount: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          const running = (res.items || [])
            .filter((t) => t.status === AiTaskStatus.Pending || t.status === AiTaskStatus.Running)
            .sort((a, b) => +new Date(b.creationTime) - +new Date(a.creationTime))[0];
          if (!running) return;
          this.aiTaskService
            .get(running.id)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
              next: (full) => this.runningAiTask.set(full),
              error: () => this.runningAiTask.set(running),
            });
        },
      });
  }

  /** 「查看进度」：打开进度面板并继续跟进后台任务。 */
  openAiProgress(task: AiGenerationTaskDto): void {
    this.aiVisible.set(true);
    this.restoreAiTask(task);
  }

  private errorText(err: any, fallback: string): string {
    return err?.error?.error?.message || err?.error?.message || fallback;
  }
}
