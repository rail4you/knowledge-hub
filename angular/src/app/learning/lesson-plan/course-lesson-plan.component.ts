import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MyCoursesService } from '../shared/my-courses.service';
import { CommonModule } from '@angular/common';
import { CourseContextService } from '../course-context.service';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
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
import { NzUploadFile, NzUploadModule } from 'ng-zorro-antd/upload';
import { Subscription, takeUntil , take } from 'rxjs';
import { Subject } from 'rxjs';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { CourseResourceService } from '../../proxy/courses/course-resource.service';
import type { ChapterDto } from '../../proxy/courses/dtos/models';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { CourseResourceDto } from '../../proxy/courses/dtos/models';
import { OssUploadService } from '../../shared/oss-upload.service';
import {
  AiGenerationTaskDto,
  AiTaskService,
  AiTaskStatus,
  AiTaskType,
} from '../../ai/services/ai-task.service';
import { AiTaskNotificationService } from '../../ai/services/ai-task-notification.service';
import { ChatService } from '../../ai/services/chat.service';
import { ChapterPickerComponent } from '../../shared/chapter-picker/chapter-picker.component';
import {
  CourseLessonPlanDto,
  CourseLessonPlanService,
  CourseLessonPlanSource,
} from './course-lesson-plan.service';

type ChapterPickerTarget = 'filter' | 'upload';

/** AI 生成教案向导步骤：选择章节 → 预览确认 → 后台生成。 */
type AiStep = 'select' | 'preview' | 'generating';

/** AI 生成待办的一个章节（勾选结果 → 预览确认 → 逐章生成）。 */
interface AiChapterItem {
  id: string;
  order: number;
  /** 章节标题（预览时可修改，随章节一起进入生成提示词）。 */
  title: string;
  /** 完整路径（如「第一章 / 1.1 概述」），预览时展示定位。 */
  path: string;
  /** 内容要点（生成该章教案的教学依据，预览时可修改）。 */
  summary: string;
  /** 本章下的子章节（含更深层级），内容会一并带入本章的生成提示词。 */
  children: AiSubChapter[];
  /** 本章学时（分钟）。 */
  duration: number;
  /** 本章参考资源（课程资源 Id，可空）。 */
  resourceId: string | null;
  /** 本章的教师附加要求（可空）。 */
  customPrompt: string;
}

/** 章节下的子章节（用于提示词补全 + 预览提示）。 */
interface AiSubChapter {
  title: string;
  /** 相对本章的路径（如「1.1 概述 / 1.1.1 起源」）。 */
  path: string;
  summary: string;
}

interface ChapterRow {
  id: string;
  title: string;
  level: number;
  path: string;
  ancestorIds: string[];
  hasChildren: boolean;
  matched: boolean;
}

interface AiLessonPlan {
  title?: string;
  subject?: string;
  grade?: string;
  duration?: number;
  objectives?: string[];
  keyPoints?: string[];
  difficulties?: string[];
  sections?: {
    name?: string;
    duration?: number;
    content?: string;
    activities?: string[];
  }[];
  methods?: string[];
  resources?: string[];
  assessment?: string[];
  homework?: string[];
}

@Component({
  selector: 'app-course-lesson-plan',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzDescriptionsModule,
    NzDividerModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzPopconfirmModule,
    NzProgressModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
    NzUploadModule,
    NzStepsModule,
    ChapterPickerComponent,
  ],
  templateUrl: './course-lesson-plan.component.html',
  styleUrls: ['./course-lesson-plan.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseLessonPlanComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
  /** 课程下拉：教师只列自己创建的课程（管理员列本校全部） */
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly chapterService = inject(ChapterService);
  private readonly courseResourceService = inject(CourseResourceService);
  private readonly lessonPlanService = inject(CourseLessonPlanService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly aiTaskService = inject(AiTaskService);
  private readonly aiTaskNotifications = inject(AiTaskNotificationService);
  private readonly chatService = inject(ChatService);
  private readonly message = inject(NzMessageService);
  private readonly destroy$ = new Subject<void>();

  readonly CourseLessonPlanSource = CourseLessonPlanSource;

  // ── 课程 / 章节 ──
  courses = signal<CourseDto[]>([]);
  selectedCourseId = signal<string | null>(null);
  chapters = signal<ChapterDto[]>([]);
  chaptersLoading = signal(false);
  courseResources = signal<CourseResourceDto[]>([]);

  // ── 章节选择器（嵌套表格 + 搜索） ──
  chapterRows = computed(() => this.buildChapterRows(this.chapters()));
  collapsedChapterIds = signal<Set<string>>(new Set());
  chapterSearch = signal('');
  chapterPickerVisible = signal(false);
  chapterPickerTarget = signal<ChapterPickerTarget>('filter');
  chapterPickerSelection = signal<string | null>(null);

  visibleChapterRows = computed(() => {
    const rows = this.chapterRows();
    const keyword = this.chapterSearch().trim().toLowerCase();
    if (!keyword) {
      const collapsed = this.collapsedChapterIds();
      return rows.filter((row) => row.ancestorIds.every((id) => !collapsed.has(id)));
    }

    const matchedIds = new Set(
      rows
        .filter((row) => row.title.toLowerCase().includes(keyword) || row.path.toLowerCase().includes(keyword))
        .map((row) => row.id),
    );
    if (matchedIds.size === 0) return [];

    const visibleIds = new Set<string>();
    rows.forEach((row) => {
      if (matchedIds.has(row.id)) {
        visibleIds.add(row.id);
        row.ancestorIds.forEach((id) => visibleIds.add(id));
      }
    });
    rows.forEach((row) => {
      if (row.ancestorIds.some((id) => matchedIds.has(id))) {
        visibleIds.add(row.id);
      }
    });

    return rows
      .filter((row) => visibleIds.has(row.id))
      .map((row) => ({ ...row, matched: matchedIds.has(row.id) }));
  });

  chapterPickerSelectedTitle = computed(() => this.chapterPathOf(this.chapterPickerSelection()) ?? '');

  chapterFilterTitle = computed(() => this.chapterPathOf(this.chapterFilter()) ?? '');

  chapterFilterDisplay = computed(() => {
    if (this.chaptersLoading()) return '加载章节…';
    if (this.chapters().length === 0) return '暂无章节';
    return this.chapterFilterTitle() || '全部章节';
  });

  selectedCourse = computed(
    () => this.courses().find((c) => c.id === this.selectedCourseId()) ?? null,
  );

  // ── AI 生成向导 ──
  /** 章节树按顺序展开（父在前、子随后），用于把勾选 id 映射为有序章节列表。 */
  private readonly flatChapterTree = computed(() => {
    const out: {
      id: string;
      title: string;
      path: string;
      summary: string;
      children: AiSubChapter[];
    }[] = [];
    const walk = (nodes: ChapterDto[] | undefined, parentPath: string) => {
      for (const n of nodes || []) {
        const title = n.title || '(未命名章节)';
        const path = parentPath ? `${parentPath} / ${title}` : title;
        out.push({
          id: n.id,
          title,
          path,
          summary: n.description || '',
          children: this.collectSubChapters(n),
        });
        walk(n.children, path);
      }
    };
    walk(this.aiChapterTree(), '');
    return out;
  });

  /**
   * 收集章节下的全部子章节（含更深层级）。
   * 勾选父章节时不会自动勾子章节，但子章节的内容会作为本章的补充要点带入生成提示词。
   */
  private collectSubChapters(node: ChapterDto): AiSubChapter[] {
    const out: AiSubChapter[] = [];
    const walk = (children: ChapterDto[] | undefined, parentPath: string) => {
      for (const c of children || []) {
        const title = c.title || '(未命名章节)';
        const path = parentPath ? `${parentPath} / ${title}` : title;
        out.push({ title, path, summary: c.description || '' });
        walk(c.children, path);
      }
    };
    walk(node.children, '');
    return out;
  }

  /** 向导步骤条索引：0=选择章节，1=预览确认，2=生成进度。 */
  readonly aiStepIndex = computed(() =>
    this.aiStep() === 'select' ? 0 : this.aiStep() === 'preview' ? 1 : 2,
  );

  /** 已用时 MM:SS（供进度面板展示）。 */
  readonly aiElapsedText = computed(() => {
    const s = this.aiElapsed();
    const m = Math.floor(s / 60);
    return `${String(m).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  });

  // ── 列表 ──
  plans = signal<CourseLessonPlanDto[]>([]);
  loading = signal(false);
  total = signal(0);
  pageIndex = signal(1);
  pageSize = signal(10);

  keyword = signal('');
  chapterFilter = signal<string | null>(null);
  sourceFilter = signal<CourseLessonPlanSource | null>(null);

  // ── 上传弹窗 ──
  uploadVisible = signal(false);
  uploading = signal(false);
  uploadTitle = signal('');
  uploadChapterId = signal<string | null>(null);
  uploadDescription = signal('');
  uploadFile = signal<File | null>(null);
  uploadFileName = signal('');

  // ── AI 生成弹窗 ──
  aiVisible = signal(false);
  /** 向导当前步骤。 */
  aiStep = signal<AiStep>('select');
  /** 章节树（勾选用，含 children）。 */
  aiChapterTree = signal<ChapterDto[]>([]);
  /** 勾选的章节 id（复选框多选）。 */
  aiSelectedChapterIds = signal<string[]>([]);
  /** 预览确认的章节（有序，要点可编辑）。 */
  aiChapters = signal<AiChapterItem[]>([]);
  aiDuration = signal(45);
  aiCustomPrompt = signal('');
  aiResourceId = signal<string | null>(null);
  aiGenerating = signal(false);
  aiProgress = signal(0);
  aiMessage = signal('');
  /** 当前正在生成的章节序号（从 1 开始）。 */
  aiActiveChapter = signal(0);
  /** 已完成生成的章节序号集合（后端进度文案「第 X/Y 章已完成」驱动）。 */
  aiDoneChapters = signal<Set<number>>(new Set());
  /** 本次任务的总章节数。 */
  aiChapterTotal = signal(0);
  /** 已用时（秒）：单章大模型调用常需 30~120 秒，用已用时让用户感知进度。 */
  aiElapsed = signal(0);
  private aiPollSub: Subscription | null = null;
  private aiTaskId: string | null = null;
  private aiTimer: ReturnType<typeof setInterval> | null = null;
  /** 全部完成后自动关闭进度面板的计时器。 */
  private aiCloseTimer: ReturnType<typeof setTimeout> | null = null;
  private aiStartedAt = 0;
  /** 进行中的后台任务（切页回来时恢复进度面板）。 */
  runningAiTask = signal<AiGenerationTaskDto | null>(null);

  // ── 预览 ──
  previewVisible = signal(false);
  previewPlan = signal<CourseLessonPlanDto | null>(null);
  previewContent = signal<AiLessonPlan | null>(null);
  exporting = signal(false);

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

    // 切页回来：恢复我名下进行中的课程多章节教案任务（后台生成，进度不丢）
    this.resumeAiTask();

    // 后台任务完成（全局通知轮询）：清掉进度横幅并刷新教案列表
    this.aiTaskNotifications.completed$
      .pipe(takeUntil(this.destroy$))
      .subscribe((tasks) => {
        const done = tasks.filter((t) => t.taskType === AiTaskType.LessonPlanCourseMulti);
        if (done.length === 0) return;
        this.runningAiTask.set(null);
        this.pageIndex.set(1);
        this.loadPlans();
      });
  }

  ngOnDestroy(): void {
    this.aiPollSub?.unsubscribe();
    this.stopAiTimer();
    this.clearAiPanelCloseTimer();
    this.destroy$.next();
    this.destroy$.complete();
  }

  /** ?taskId= 深链：拉取任务并恢复进度面板（已完成则刷新列表）。 */
  private loadAiTaskPreview(taskId: string): void {
    this.aiTaskService.get(taskId).subscribe({
      next: (task) => {
        if (task.taskType !== AiTaskType.LessonPlanCourseMulti) return;
        if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
          this.aiVisible.set(true);
          this.restoreAiTask(task);
        } else if (task.status === AiTaskStatus.Completed) {
          this.message.success('AI 教案已生成');
          this.pageIndex.set(1);
          this.loadPlans();
        }
      },
      error: () => undefined,
    });
  }

  loadCourses(): void {
    this.myCourses.getCourses(300).subscribe({
      next: (result) => {
        const items = result || [];
        this.courses.set(items as any);
        if (items.length > 0 && !this.selectedCourseId()) {
          this.onCourseChange(items[0].id);
        }
      },
      error: () => this.message.error('加载课程列表失败'),
    });
  }

  onCourseChange(courseId: string | null): void {
    this.courseContext.setCourse(courseId);
    this.selectedCourseId.set(courseId);
    this.chapters.set([]);
    this.chaptersLoading.set(false);
    this.courseResources.set([]);
    this.chapterFilter.set(null);
    this.pageIndex.set(1);
    if (!courseId) {
      this.plans.set([]);
      this.total.set(0);
      return;
    }

    this.chaptersLoading.set(true);
    this.chapterService.getChaptersByCourse(courseId).subscribe({
      next: (list) => {
        this.chapters.set(list || []);
        this.chaptersLoading.set(false);
      },
      error: () => {
        this.chapters.set([]);
        this.chaptersLoading.set(false);
      },
    });

    this.courseResourceService.getByCourse(courseId).subscribe({
      next: (list) => this.courseResources.set(list || []),
      error: () => this.courseResources.set([]),
    });

    this.loadPlans();
  }

  loadPlans(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.lessonPlanService
      .getList({
        courseId,
        chapterId: this.chapterFilter() || undefined,
        source: this.sourceFilter() ?? undefined,
        filter: this.keyword().trim() || undefined,
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.plans.set(result.items || []);
          this.total.set(result.totalCount || 0);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载教案列表失败');
        },
      });
  }

  onKeywordChange(value: string): void {
    this.keyword.set(value);
    this.pageIndex.set(1);
    this.loadPlans();
  }

  onChapterFilterChange(value: string | null): void {
    this.chapterFilter.set(value);
    this.pageIndex.set(1);
    this.loadPlans();
  }

  // ── 章节选择器（列表筛选 / 上传教案归属，单选） ──
  openChapterPicker(target: ChapterPickerTarget): void {
    if (this.chaptersLoading()) return;
    if (this.chapters().length === 0) {
      this.message.warning('当前课程还没有章节');
      return;
    }
    const current = target === 'filter' ? this.chapterFilter() : this.uploadChapterId();
    this.chapterPickerTarget.set(target);
    this.chapterPickerSelection.set(current);
    this.chapterSearch.set('');
    this.collapsedChapterIds.set(new Set());
    this.chapterPickerVisible.set(true);
  }

  selectChapterRow(row: ChapterRow): void {
    this.chapterPickerSelection.set(row.id);
  }

  clearChapterPickerSelection(): void {
    this.chapterPickerSelection.set(null);
  }

  confirmChapterPicker(): void {
    const target = this.chapterPickerTarget();
    const chapterId = this.chapterPickerSelection();
    if (target === 'filter') {
      this.onChapterFilterChange(chapterId);
    } else {
      this.uploadChapterId.set(chapterId);
    }
    this.chapterPickerVisible.set(false);
  }

  cancelChapterPicker(): void {
    this.chapterPickerVisible.set(false);
  }

  toggleChapterRow(row: ChapterRow): void {
    if (!row.hasChildren) return;
    const next = new Set(this.collapsedChapterIds());
    if (next.has(row.id)) {
      next.delete(row.id);
    } else {
      next.add(row.id);
    }
    this.collapsedChapterIds.set(next);
  }

  isChapterCollapsed(row: ChapterRow): boolean {
    return this.collapsedChapterIds().has(row.id);
  }

  expandAllChapters(): void {
    this.collapsedChapterIds.set(new Set());
  }

  collapseAllChapters(): void {
    const ids = new Set(this.chapterRows().filter((row) => row.hasChildren).map((row) => row.id));
    this.collapsedChapterIds.set(ids);
  }

  onSourceFilterChange(value: CourseLessonPlanSource | null): void {
    this.sourceFilter.set(value);
    this.pageIndex.set(1);
    this.loadPlans();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.loadPlans();
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
    this.loadPlans();
  }

  // ── 上传 ──
  openUpload(): void {
    if (!this.selectedCourseId()) {
      this.message.warning('请先选择课程');
      return;
    }
    this.uploadTitle.set('');
    this.uploadChapterId.set(null);
    this.uploadDescription.set('');
    this.uploadFile.set(null);
    this.uploadFileName.set('');
    this.uploadVisible.set(true);
  }

  beforeUpload = (file: NzUploadFile): boolean => {
    const raw = (file.originFileObj as File | undefined) ?? (file as unknown as File);
    this.uploadFile.set(raw);
    this.uploadFileName.set(raw.name);
    if (!this.uploadTitle().trim()) {
      this.uploadTitle.set(raw.name.replace(/\.[^.]+$/, ''));
    }
    return false;
  };

  clearUploadFile(): void {
    this.uploadFile.set(null);
    this.uploadFileName.set('');
  }

  submitUpload(): void {
    const courseId = this.selectedCourseId();
    const file = this.uploadFile();
    if (!courseId) return;
    if (!file) {
      this.message.warning('请选择教案文件');
      return;
    }
    if (!this.uploadTitle().trim()) {
      this.message.warning('请填写教案标题');
      return;
    }

    this.uploading.set(true);
    this.ossUploadService.uploadFile(file).subscribe({
      next: (res) => {
        const extension = file.name.includes('.')
          ? '.' + file.name.split('.').pop()!.toLowerCase()
          : undefined;
        this.lessonPlanService
          .create({
            courseId,
            chapterId: this.uploadChapterId() || undefined,
            chapterTitle: this.chapterTitleOf(this.uploadChapterId()),
            title: this.uploadTitle().trim(),
            description: this.uploadDescription().trim() || undefined,
            fileUrl: res.url,
            fileName: file.name,
            fileExtension: extension,
            fileSize: file.size,
          })
          .subscribe({
            next: () => {
              this.uploading.set(false);
              this.uploadVisible.set(false);
              this.message.success('教案已上传');
              this.pageIndex.set(1);
              this.loadPlans();
            },
            error: (err) => {
              this.uploading.set(false);
              this.message.error(this.errorText(err, '保存教案失败'));
            },
          });
      },
      error: (err) => {
        this.uploading.set(false);
        this.message.error(this.errorText(err, '文件上传失败'));
      },
    });
  }

  // ── AI 生成（多章节：勾选章节 → 预览确认 → 后台逐章生成） ──
  openAiGenerate(): void {
    if (!this.selectedCourseId()) {
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
      this.message.info('已有教案生成任务在进行中，先查看进度');
      this.openAiProgress(running);
      return;
    }
    this.aiStep.set('select');
    this.aiSelectedChapterIds.set([]);
    this.aiChapters.set([]);
    this.aiDuration.set(45);
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

  /** 拉取课程章节树（勾选用，带 children 层级）。 */
  private loadAiChapterTree(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.chapterService.getChapterTree(courseId).subscribe({
      next: (tree) => this.aiChapterTree.set(tree || []),
      error: () => this.aiChapterTree.set([]),
    });
  }

  /** 勾选章节（复选框多选，父子联动由章节选择器组件处理）。 */
  onAiSelectedIdsChange(ids: string[]): void {
    this.aiSelectedChapterIds.set(ids);
  }

  /** 选择章节 → 预览确认。 */
  aiNextToPreview(): void {
    const selected = new Set(this.aiSelectedChapterIds());
    // 保留上一次在预览步骤里编辑过的内容（返回上一步再进来时不丢）
    const edited = new Map(this.aiChapters().map((c) => [c.id, c]));
    const list = this.flatChapterTree()
      .filter((c) => selected.has(c.id))
      .map((c, i) => {
        const prev = edited.get(c.id);
        return {
          ...c,
          order: i + 1,
          // 标题可编辑；未填写过则用章节原标题
          title: prev?.title ?? c.title,
          // 内容要点：优先用章节描述；没有描述时用章节标题兜底（避免生成时缺少本章标题）
          summary: prev?.summary ?? (c.summary || c.title),
          children: c.children,
          duration: prev?.duration ?? this.aiDuration(),
          resourceId: prev?.resourceId ?? this.aiResourceId(),
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

  /** 预览步骤：编辑章节标题（随章节进入生成提示词）。 */
  updateAiChapterTitle(id: string, value: string): void {
    this.aiChapters.update((list) => list.map((c) => (c.id === id ? { ...c, title: value } : c)));
  }

  /** 预览步骤：编辑章节要点（生成该章教案的教学依据）。 */
  updateAiChapterSummary(id: string, value: string): void {
    this.aiChapters.update((list) => list.map((c) => (c.id === id ? { ...c, summary: value } : c)));
  }

  /** 预览步骤：编辑本章学时。 */
  updateAiChapterDuration(id: string, value: number): void {
    this.aiChapters.update((list) => list.map((c) => (c.id === id ? { ...c, duration: value } : c)));
  }

  /** 预览步骤：选择本章参考资源。 */
  updateAiChapterResource(id: string, value: string | null): void {
    this.aiChapters.update((list) => list.map((c) => (c.id === id ? { ...c, resourceId: value } : c)));
  }

  /** 预览步骤：编辑本章教师附加要求。 */
  updateAiChapterPrompt(id: string, value: string): void {
    this.aiChapters.update((list) => list.map((c) => (c.id === id ? { ...c, customPrompt: value } : c)));
  }

  /** 「统一设置」应用到全部章节：学时 / 参考资源 / 附加要求。 */
  applyAiDefaultsToAll(): void {
    const duration = this.aiDuration();
    const resourceId = this.aiResourceId();
    const customPrompt = this.aiCustomPrompt();
    this.aiChapters.update((list) =>
      list.map((c) => ({ ...c, duration, resourceId, customPrompt })),
    );
    this.message.success('已应用到全部章节');
  }

  /** 预览步骤：移除某个章节。 */
  removeAiChapter(id: string): void {
    this.aiChapters.update((list) =>
      list.filter((c) => c.id !== id).map((c, i) => ({ ...c, order: i + 1 })),
    );
    this.aiSelectedChapterIds.update((ids) => ids.filter((x) => x !== id));
  }

  /** 确认生成：提交后台任务（逐章生成，每章落一条教案）。 */
  submitAiGenerate(): void {
    const course = this.selectedCourse();
    if (!course) return;
    const chapters = this.aiChapters();
    if (chapters.length === 0) {
      this.message.warning('请至少勾选一个章节');
      return;
    }

    const resource = this.courseResources().find((r) => r.resourceId === this.aiResourceId());

    const input = {
      courseId: course.id,
      courseTitle: course.title,
      duration: this.aiDuration() || 45,
      customPrompt: this.aiCustomPrompt().trim() || undefined,
      resourceId: this.aiResourceId() || undefined,
      chapters: chapters.map((c, i) => ({
        chapterId: c.id,
        order: i + 1,
        title: c.title.trim(),
        summary: c.summary?.trim() || undefined,
        // 本章学时 / 参考资源 / 附加要求（以章节为基础，逐章可单独设置）
        duration: c.duration || this.aiDuration() || 45,
        resourceId: c.resourceId || undefined,
        customPrompt: c.customPrompt?.trim() || undefined,
        // 子章节内容：随本章一起送进生成提示词（勾父章节不会自动勾子章节）
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
        taskType: AiTaskType.LessonPlanCourseMulti,
        title: `${course.title} - ${chapters.length} 章教案`,
        resourceId: resource?.resourceId,
        resourceName: resource?.resourceName || course.title,
        inputJson: JSON.stringify(input),
      })
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
          // 全部章节都完成了：逐章标记为已完成（后端最终进度文案只保留“已完成”，
          // 只靠文案回推会漏掉最后一章），然后自动关闭进度面板回到教案列表
          this.markAllAiChaptersDone();
          this.aiMessage.set(`全部 ${this.aiChapterTotal()} 章教案已生成`);
          this.runningAiTask.set(null);
          this.message.success(`AI 教案已生成（${this.aiChapterTotal()} 章）`);
          this.pageIndex.set(1);
          this.loadPlans();
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

  /**
   * 从进度文案解析当前章节号（任务实体只持久化文案，章节号需在此解析）。
   * 文案形如：`正在生成第 2/6 章：标题`、`第 2/6 章已完成：标题`。
   */
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
      // 该章已生成完成 → 记入完成集合，并把「当前章」推进到下一个未完成的章节
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
    const total = this.aiChapterTotal() || this.chapters().length;
    const done = new Set<number>();
    for (let i = 1; i <= total; i++) done.add(i);
    this.aiDoneChapters.set(done);
    this.aiActiveChapter.set(0);
  }

  /** 全部完成后自动关闭进度面板，回到教案列表（稍作停留让用户看到完成态）。 */
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

  /** 取消生成：取消后台任务，回到预览步骤。 */
  cancelAiGenerate(): void {
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

  /** 关闭弹窗（生成中也可关闭：任务在后台继续跑，回来可看进度）。 */
  closeAiModal(): void {
    this.aiVisible.set(false);
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

  /** 从任务 inputJson 还原章节列表（供恢复进度面板展示）。 */
  private parseAiChaptersFromInput(inputJson?: string): AiChapterItem[] {
    if (!inputJson) return [];
    try {
      const input = JSON.parse(inputJson) as {
        duration?: number;
        resourceId?: string;
        customPrompt?: string;
        chapters?: {
          chapterId?: string;
          order?: number;
          title?: string;
          summary?: string;
          duration?: number;
          resourceId?: string;
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
          duration: c.duration || input.duration || 45,
          resourceId: c.resourceId || input.resourceId || null,
          customPrompt: c.customPrompt ?? input.customPrompt ?? '',
        }));
    } catch {
      return [];
    }
  }

  /** 查询我名下进行中的课程多章节教案任务（切页回来也能看到生成中进度）。 */
  private resumeAiTask(): void {
    this.aiTaskService
      .getList({ taskType: AiTaskType.LessonPlanCourseMulti, onlyMine: true, maxResultCount: 20 })
      .subscribe({
        next: (res) => {
          const running = (res.items || [])
            .filter((t) => t.status === AiTaskStatus.Pending || t.status === AiTaskStatus.Running)
            .sort((a, b) => +new Date(b.creationTime) - +new Date(a.creationTime))[0];
          if (!running) return;
          // 列表接口不返回 inputJson（章节清单），取详情补齐，便于恢复进度面板
          this.aiTaskService.get(running.id).subscribe({
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

  // ── 预览 / 下载 / 删除 ──
  preview(plan: CourseLessonPlanDto): void {
    if (plan.source === CourseLessonPlanSource.Ai) {
      this.previewPlan.set(plan);
      this.previewContent.set(this.parseContent(plan.contentJson));
      this.previewVisible.set(true);
      return;
    }

    if (plan.fileUrl) {
      window.open(plan.fileUrl, '_blank');
    } else {
      this.message.warning('该教案没有可预览的文件');
    }
  }

  download(plan: CourseLessonPlanDto): void {
    if (plan.source === CourseLessonPlanSource.Ai && plan.contentJson) {
      this.exporting.set(true);
      this.chatService
        .exportLessonPlanDocx(plan.contentJson)
        .then((blob) => {
          this.saveBlob(blob, `${plan.title || '教案'}.docx`);
          this.message.success('教案已下载');
        })
        .catch(() => this.message.error('导出失败，请重试'))
        .finally(() => this.exporting.set(false));
      return;
    }

    if (plan.fileUrl) {
      const a = document.createElement('a');
      a.href = plan.fileUrl;
      a.download = plan.fileName || plan.title;
      a.target = '_blank';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      return;
    }

    this.message.warning('该教案没有可下载的文件');
  }

  remove(plan: CourseLessonPlanDto): void {
    this.lessonPlanService.delete(plan.id).subscribe({
      next: () => {
        this.message.success('已删除');
        this.loadPlans();
      },
      error: () => this.message.error('删除失败'),
    });
  }

  closePreview(): void {
    this.previewVisible.set(false);
    this.previewPlan.set(null);
    this.previewContent.set(null);
  }

  // ── helpers ──
  private buildChapterRows(chapters: ChapterDto[]): ChapterRow[] {
    const byId = new Map<string, ChapterDto>();
    chapters.forEach((chapter) => byId.set(chapter.id, chapter));

    const childrenMap = new Map<string, ChapterDto[]>();
    const roots: ChapterDto[] = [];
    chapters.forEach((chapter) => {
      const parentId = chapter.parentId && byId.has(chapter.parentId) ? chapter.parentId : null;
      if (!parentId) {
        roots.push(chapter);
        return;
      }
      const siblings = childrenMap.get(parentId) ?? [];
      siblings.push(chapter);
      childrenMap.set(parentId, siblings);
    });

    const sortByOrder = (a: ChapterDto, b: ChapterDto) =>
      (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || (a.title ?? '').localeCompare(b.title ?? '');
    roots.sort(sortByOrder);
    childrenMap.forEach((siblings) => siblings.sort(sortByOrder));

    const rows: ChapterRow[] = [];
    const walk = (nodes: ChapterDto[], level: number, parentPath: string, ancestorIds: string[]) => {
      nodes.forEach((node) => {
        const title = node.title || '(未命名章节)';
        const path = parentPath ? `${parentPath} / ${title}` : title;
        rows.push({
          id: node.id,
          title,
          level,
          path,
          ancestorIds: [...ancestorIds],
          hasChildren: (childrenMap.get(node.id)?.length ?? 0) > 0,
          matched: false,
        });
        walk(childrenMap.get(node.id) ?? [], level + 1, path, [...ancestorIds, node.id]);
      });
    };
    walk(roots, 0, '', []);
    return rows;
  }

  chapterPathOf(chapterId: string | null): string | undefined {
    if (!chapterId) return undefined;
    return this.chapterRows().find((row) => row.id === chapterId)?.path;
  }

  chapterTitleOf(chapterId: string | null): string | undefined {
    if (!chapterId) return undefined;
    return this.chapters().find((c) => c.id === chapterId)?.title;
  }

  sourceLabel(source: CourseLessonPlanSource): string {
    return source === CourseLessonPlanSource.Ai ? 'AI 生成' : '上传';
  }

  formatFileSize(size?: number): string {
    if (!size) return '';
    if (size < 1024) return `${size} B`;
    if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
    return `${(size / 1024 / 1024).toFixed(1)} MB`;
  }

  private parseContent(json?: string): AiLessonPlan | null {
    if (!json) return null;
    try {
      return JSON.parse(json) as AiLessonPlan;
    } catch {
      return null;
    }
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  private errorText(err: any, fallback: string): string {
    return (
      err?.error?.error?.message ||
      err?.error?.message ||
      err?.message ||
      fallback
    );
  }
}
