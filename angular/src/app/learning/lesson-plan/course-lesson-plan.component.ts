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
import {
  CourseLessonPlanDto,
  CourseLessonPlanService,
  CourseLessonPlanSource,
} from './course-lesson-plan.service';

type ChapterPickerTarget = 'filter' | 'upload' | 'ai';

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
  ],
  templateUrl: './course-lesson-plan.component.html',
  styleUrls: ['./course-lesson-plan.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseLessonPlanComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
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
  chapterPickerTarget = signal<ChapterPickerTarget>('ai');
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
  aiChapterId = signal<string | null>(null);
  aiDuration = signal(45);
  aiCustomPrompt = signal('');
  aiResourceId = signal<string | null>(null);
  aiGenerating = signal(false);
  aiProgress = signal(0);
  aiMessage = signal('');
  private aiPollSub: Subscription | null = null;
  private aiTaskId: string | null = null;

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
    });
  }

  ngOnDestroy(): void {
    this.aiPollSub?.unsubscribe();
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadCourses(): void {
    this.courseService.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe({
      next: (result) => {
        const items = result.items || [];
        this.courses.set(items);
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

  // ── 章节选择器 ──
  openChapterPicker(target: ChapterPickerTarget): void {
    if (target === 'ai' && this.aiGenerating()) return;
    if (this.chaptersLoading()) return;
    if (this.chapters().length === 0) {
      this.message.warning('当前课程还没有章节');
      return;
    }
    const current =
      target === 'filter'
        ? this.chapterFilter()
        : target === 'upload'
          ? this.uploadChapterId()
          : this.aiChapterId();
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
    if (target === 'ai' && !chapterId) {
      this.message.warning('请选择课程章节');
      return;
    }
    if (target === 'filter') {
      this.onChapterFilterChange(chapterId);
    } else if (target === 'upload') {
      this.uploadChapterId.set(chapterId);
    } else {
      this.aiChapterId.set(chapterId);
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

  // ── AI 生成 ──
  openAiGenerate(): void {
    if (!this.selectedCourseId()) {
      this.message.warning('请先选择课程');
      return;
    }
    if (this.chapters().length === 0) {
      this.message.warning('当前课程还没有章节，请先在「章节管理」中维护章节');
      return;
    }
    this.aiChapterId.set(this.chapters()[0]?.id ?? null);
    this.aiDuration.set(45);
    this.aiCustomPrompt.set('');
    this.aiResourceId.set(null);
    this.aiGenerating.set(false);
    this.aiProgress.set(0);
    this.aiMessage.set('');
    this.aiVisible.set(true);
  }

  submitAiGenerate(): void {
    const course = this.selectedCourse();
    const chapterId = this.aiChapterId();
    if (!course) return;
    if (!chapterId) {
      this.message.warning('请选择课程章节');
      return;
    }

    const chapter = this.chapters().find((c) => c.id === chapterId);
    const resource = this.courseResources().find((r) => r.resourceId === this.aiResourceId());

    const input = {
      resourceId: this.aiResourceId() || null,
      topic: chapter?.title || '',
      duration: this.aiDuration() || 45,
      customPrompt: this.aiCustomPrompt().trim() || undefined,
      courseId: course.id,
      courseTitle: course.title,
      chapterId,
      chapterTitle: chapter?.title || '',
      chapterSummary: chapter?.description || undefined,
    };

    this.aiGenerating.set(true);
    this.aiProgress.set(0);
    this.aiMessage.set('任务已提交，正在后台生成…');

    this.aiTaskService
      .create({
        taskType: AiTaskType.LessonPlanSingle,
        title: `${course.title} - ${chapter?.title || 'AI 教案'}`,
        resourceId: resource?.resourceId,
        resourceName: resource?.resourceName || chapter?.title,
        inputJson: JSON.stringify(input),
      })
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
          this.message.success('AI 教案已生成');
          this.pageIndex.set(1);
          this.loadPlans();
        } else if (
          task.status === AiTaskStatus.Failed ||
          task.status === AiTaskStatus.Cancelled
        ) {
          this.aiPollSub?.unsubscribe();
          this.aiPollSub = null;
          this.aiGenerating.set(false);
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

  cancelAiGenerate(): void {
    if (this.aiTaskId) {
      this.aiTaskService.cancel(this.aiTaskId).subscribe({ next: () => {}, error: () => {} });
    }
    this.aiPollSub?.unsubscribe();
    this.aiPollSub = null;
    this.aiGenerating.set(false);
    this.aiVisible.set(false);
    this.aiTaskId = null;
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
