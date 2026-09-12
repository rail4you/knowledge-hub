import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import type { CourseDetailDto, ChapterDto, KnowledgeResourceDto } from '../../proxy/courses/dtos/models';
import type { ExerciseDto } from '../../proxy/exams/dtos/models';
import type { StudentExerciseRecordDto } from '../../proxy/learning/dtos/models';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';
import { SelfAssessment } from '../../proxy/learning/enums/self-assessment.enum';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { buildDownloadFileName } from '../../shared/download/download-file.util';
import { VoiceContextService } from '../voice/voice-context.service';

type TabKey = 'resources' | 'exercises' | 'submissions';

interface OptionItem {
  key: string;
  content: string;
}

/** 模板中频繁使用的静态映射，提到模块级避免每次变更检测重复分配 */
const DIFFICULTY_LABELS = ['入门', '初级', '中级', '高级', '专家'];
const IMPORTANCE_LABELS: Record<string, string> = {
  core: '核心',
  important: '重要',
  normal: '一般',
  extended: '拓展',
};
const IMPORTANCE_COLORS: Record<string, string> = {
  core: '#ef4444',
  important: '#f59e0b',
  normal: '#2b6cd4',
  extended: '#10b981',
};
const EXERCISE_TYPE_LABELS = ['单选题', '多选题', '判断题', '填空题', '简答题', '论述题', '案例分析'];

interface FlatChapter {
  id: string;
  title: string;
  depth: number;
  exerciseCount: number;
  resourceCount: number;
  parentId: string | null;
}

@Component({
  selector: 'app-student-course-learn',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzRadioModule,
    NzCheckboxModule,
    NzInputModule,
    NzPaginationModule,
    NzSelectModule,
    NzTableModule,
    FilePreviewComponent,
  ],
  templateUrl: './student-course-learn.component.html',
  styleUrls: ['./student-course-learn.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentCourseLearnComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);
  private readonly learningService = inject(LearningService);
  private readonly exerciseService = inject(ExerciseService);
  private readonly recordService = inject(StudentExerciseRecordService);
  private readonly message = inject(NzMessageService);
  private readonly voiceContext = inject(VoiceContextService);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  readonly loading = signal(true);
  readonly course = signal<CourseDetailDto | null>(null);
  readonly chapters = signal<ChapterDto[]>([]);
  readonly flatChapters = signal<FlatChapter[]>([]);
  readonly currentChapterId = signal<string | null>(null);
  readonly expandedNodes = signal<Set<string>>(new Set());

  // 左侧章节工具条：关键词搜索 + 仅看有资源的章节
  readonly chapterKeyword = signal('');
  readonly onlyWithResources = signal(false);
  readonly isChapterFiltering = computed(
    () => this.chapterKeyword().trim() !== '' || this.onlyWithResources()
  );

  /** 过滤后的章节树：保留命中节点及其祖先链；筛选状态下模板自动全展开 */
  readonly visibleChapters = computed(() => {
    const kw = this.chapterKeyword().trim().toLowerCase();
    const onlyRes = this.onlyWithResources();
    if (!kw && !onlyRes) return this.chapters();
    const filter = (nodes: ChapterDto[]): ChapterDto[] => {
      const out: ChapterDto[] = [];
      for (const n of nodes || []) {
        const children = filter(n.children || []);
        if (this.chapterSelfVisible(n, kw, onlyRes) || children.length > 0) {
          out.push({ ...n, children });
        }
      }
      return out;
    };
    return filter(this.chapters());
  });

  /** 各章节内容数（直挂资源数 + 习题总数），供章节树徽标使用 */
  readonly chapterContentCountMap = computed(() => {
    const map = new Map<string, number>();
    const progress = this.chapterProgressMap();
    const walk = (nodes: ChapterDto[]) => {
      for (const n of nodes || []) {
        if (!n.id) continue;
        map.set(n.id, (n.knowledgeResources || []).length + (progress.get(n.id)?.total || 0));
        if (n.children?.length) walk(n.children);
      }
    };
    walk(this.chapters());
    return map;
  });
  /** 直接挂有学习资源或习题的章节数（用于筛选文案） */
  readonly contentChapterCount = computed(() => {
    let count = 0;
    const walk = (nodes: ChapterDto[]) => {
      for (const n of nodes || []) {
        if (this.chapterHasContent(n)) count++;
        if (n.children?.length) walk(n.children);
      }
    };
    walk(this.chapters());
    return count;
  });

  readonly activeTab = signal<TabKey>('resources');

  // 资源
  readonly currentResources = signal<KnowledgeResourceDto[]>([]);
  readonly resourcesLoading = signal(false);

  // 预览状态
  readonly previewResourceId = signal<string | null>(null);
  readonly previewTitle = signal<string>('');

  // 分页
  readonly resourcePage = signal(1);
  readonly resourcePageSize = signal(10);

  // 习题
  readonly currentExercises = signal<ExerciseDto[]>([]);
  readonly currentExercise = signal<ExerciseDto | null>(null);
  readonly exercisesLoading = signal(false);

  // 作答状态
  readonly currentAnswer = signal('');
  readonly multiSelected = signal<Set<string>>(new Set());
  readonly submittedRecord = signal<StudentExerciseRecordDto | null>(null);
  readonly hasViewedAnswer = signal(false);
  readonly submitting = signal(false);
  readonly selfAssessment = signal<SelfAssessment>(SelfAssessment.None);


  // 提交记录
  readonly chapterRecords = signal<StudentExerciseRecordDto[]>([]);
  readonly recordsLoading = signal(false);

  /** 当前章节已作答的习题 id 集合：模板判断避免每次变更检测重复遍历记录 */
  private readonly answeredExerciseIds = computed(() => {
    const ids = new Set<string>();
    for (const r of this.chapterRecords()) {
      if (r.exerciseId) ids.add(r.exerciseId);
    }
    return ids;
  });

  /** 选项解析缓存：同一 options 字符串只 JSON.parse 一次 */
  private readonly optionsCache = new Map<string, OptionItem[]>();

  // 进度
  readonly chapterStartTime = signal<number>(Date.now());
  readonly exerciseStartTime = signal<number>(Date.now());
  readonly chapterProgress = signal<number>(0);

  readonly ExerciseType = ExerciseType;
  readonly SelfAssessment = SelfAssessment;

  readonly currentChapter = computed<ChapterDto | null>(() => {
    const id = this.currentChapterId();
    if (!id) return null;
    return this.findChapter(this.chapters(), id);
  });

  readonly completedCount = signal(0);
  readonly totalExercises = signal(0);
  /** 按章节统计：每章的习题总数和已提交数 */
  readonly chapterProgressMap = signal<Map<string, { total: number; completed: number }>>(new Map());

  readonly courseProgress = computed(() => {
    const map = this.chapterProgressMap();
    let total = 0, completed = 0;
    for (const v of map.values()) {
      total += v.total;
      completed += v.completed;
    }
    if (total === 0) return 0;
    return Math.min(Math.round((completed / total) * 100), 100);
  });

  /** 已完成章节数：某章的所有习题都已提交 */
  readonly masteredChapterCount = computed(() => {
    let count = 0;
    for (const v of this.chapterProgressMap().values()) {
      if (v.total > 0 && v.completed >= v.total) count++;
    }
    return count;
  });

  /** 有习题的总章节数 */
  readonly totalChapterExercises = computed(() => {
    let count = 0;
    for (const v of this.chapterProgressMap().values()) {
      if (v.total > 0) count++;
    }
    return count;
  });

  ngOnInit() {
    const courseId = this.route.snapshot.paramMap.get('id');
    const chapterId = this.route.snapshot.paramMap.get('chapterId');
    if (!courseId) {
      this.router.navigate(['/student/courses']);
      return;
    }
    // 语音助手：注册本页上下文（只读摘要，供总结/朗读/本页问答用）。
    this.voiceContext.register('course-learn', () => {
      const course = this.course();
      const chapter = this.currentChapter();
      const resources = this.currentResources();
      const exercises = this.currentExercises();
      const flat = this.flatChapters();
      const chapterTitles = flat
        .slice(0, 12)
        .map(f => f.title)
        .join('；');
      return {
        key: 'course-learn',
        route: this.router.url,
        title: course ? `《${course.title || '未命名'}》章节学习` : '章节学习',
        summary:
          `${course ? `课程《${course.title}》` : '本课程'}，共${flat.length}个章节，习题进度${this.courseProgress()}%。` +
          `${chapter ? `当前章节：${chapter.title || '未命名'}${chapter.description ? `，${chapter.description.slice(0, 200)}` : ''}。` : ''}` +
          `本章有${resources.length}个学习资源${resources.length ? `：${resources.slice(0, 8).map(r => r.name || '未命名资源').join('；')}` : ''}。` +
          `本章有${exercises.length}道习题。` +
          `${chapterTitles ? `全课程章节有：${chapterTitles}。` : ''}`,
        items: [],
        courseId: course?.id ?? courseId,
        chapterId: this.currentChapterId(),
      };
    });
    this.loadCourse(courseId);
    this.loadChapters(courseId, chapterId);
  }

  ngOnDestroy() {
    this.voiceContext.unregister('course-learn');
    this.recordChapterProgress(true);
  }

  loadCourse(id: string) {
    this.courseService.getDetail(id).subscribe({
      next: result => {
        // 未选课（含跨租户无选课可能的课程）禁止进入学习页：明确提示后退回详情
        if (result && !result.isEnrolled) {
          this.loading.set(false);
          this.message.error('未选课，不能访问该课程学习页');
          this.router.navigate(['/student/courses', id], { queryParamsHandling: 'preserve' });
          return;
        }
        this.course.set(result);
        // 若选中章节时课程尚未就绪导致记录被跳过，此处补加载
        const currentChapterId = this.currentChapterId();
        if (currentChapterId && !this.recordsLoading()) {
          this.loadChapterRecords(currentChapterId);
        }
        this.recordChapterProgress();
      },
      error: () => {
        this.message.error('课程加载失败');
        this.router.navigate(['/student/courses']);
      },
    });
  }

  loadChapters(courseId: string, preselectId?: string | null) {
    this.chapterService.getChapterTree(courseId).subscribe({
      next: data => {
        const list = data || [];
        this.chapters.set(list);
        const flat = this.flattenChapters(list);
        this.flatChapters.set(flat);
        // 只展开第一级章节（depth===0），与课程目录页一致
        const expanded = new Set<string>();
        flat.forEach(c => { if (c.depth === 0) expanded.add(c.id); });
        this.expandedNodes.set(expanded);
        // 选中目标章节
        if (preselectId && flat.find(c => c.id === preselectId)) {
          this.selectChapter(preselectId, true);
        } else if (flat.length > 0) {
          this.selectChapter(flat[0].id);
        }
        this.loading.set(false);
        this.loadAllExercises(courseId, flat);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private flattenChapters(list: ChapterDto[], depth = 0, parentId: string | null = null): FlatChapter[] {
    const out: FlatChapter[] = [];
    list.forEach(c => {
      out.push({
        id: c.id!,
        title: c.title || '未命名章节',
        depth,
        exerciseCount: (c.knowledgeResources || []).length,
        resourceCount: (c.knowledgeResources || []).length,
        parentId,
      });
      if (c.children?.length) {
        out.push(...this.flattenChapters(c.children, depth + 1, c.id));
      }
    });
    return out;
  }

  private findChapter(nodes: ChapterDto[], id: string): ChapterDto | null {
    for (const n of nodes) {
      if (n.id === id) return n;
      if (n.children) {
        const found = this.findChapter(n.children, id);
        if (found) return found;
      }
    }
    return null;
  }

  selectChapter(id: string, expandParents = false) {
    if (this.currentChapterId() === id) return;
    this.recordChapterProgress(true);
    this.currentChapterId.set(id);
    this.chapterStartTime.set(Date.now());
    this.chapterProgress.set(0);
    this.activeTab.set('resources');
    this.loadChapterContent(id);
    this.loadChapterRecords(id);
    // 仅在初始导航时展开所有父级（保证 URL 指定的章节可见）
    // 用户手动点击时不展开，避免覆盖用户的折叠操作
    if (expandParents) {
      this.expandAncestors(id);
    }
    // 同步 URL
    const course = this.course();
    if (course?.id) {
      // 保留 fromMicroMajor 等来源参数，各章节之间切换时返回链不中断
      this.router.navigate(['/student/courses', course.id, 'learn', id], {
        replaceUrl: true,
        queryParamsHandling: 'preserve',
      });
    }
    this.recordChapterProgress();
  }

  /** 章节项点击：选中该章节；若是父级则顺便展开，避免下级目录“看不见” */
  onChapterItemClick(node: ChapterDto): void {
    if (!node.id) return;
    if (node.children?.length && !this.expandedNodes().has(node.id)) {
      const set = new Set(this.expandedNodes());
      set.add(node.id);
      this.expandedNodes.set(set);
    }
    this.selectChapter(node.id);
  }

  /** 切换折叠状态 */
  toggleChapter(event: MouseEvent, id: string): void {
    event.stopPropagation();
    const set = new Set(this.expandedNodes());
    if (set.has(id)) {
      set.delete(id);
    } else {
      set.add(id);
    }
    this.expandedNodes.set(set);
  }

  /** 单个章节自身是否满足当前筛选条件（祖先链由 visibleChapters 保留） */
  private chapterSelfVisible(node: ChapterDto, kw: string, onlyRes: boolean): boolean {
    if (onlyRes && !this.chapterHasContent(node)) return false;
    if (kw && !(node.title || '').toLowerCase().includes(kw)) return false;
    return true;
  }

  /** 自评掌握程度文案 */
  selfAssessmentLabel(v?: SelfAssessment | null): string {
    switch (v) {
      case SelfAssessment.Incorrect: return '未掌握';
      case SelfAssessment.PartiallyCorrect: return '部分掌握';
      case SelfAssessment.Correct: return '已掌握';
      default: return '—';
    }
  }
  /** 章节是否有内容：直挂资源，或习题总数 >0（习题统计异步到达后自动更新） */
  private chapterHasContent(node: ChapterDto): boolean {
    if ((node.knowledgeResources || []).length > 0) return true;
    const stat = node.id ? this.chapterProgressMap().get(node.id) : undefined;
    return !!stat && stat.total > 0;
  }

  /** 清空章节搜索与筛选 */
  clearChapterFilter(): void {
    this.chapterKeyword.set('');
    this.onlyWithResources.set(false);
  }

  /** 展开某节点的所有祖先 */
  private expandAncestors(id: string): void {
    const map = new Map(this.flatChapters().map(c => [c.id, c]));
    const set = new Set(this.expandedNodes());
    let cur = map.get(id);
    while (cur?.parentId) {
      set.add(cur.parentId);
      cur = map.get(cur.parentId);
    }
    this.expandedNodes.set(set);
  }

  private recordChapterProgress(force = false) {
    const course = this.course();
    const chapterId = this.currentChapterId();
    if (!course?.id || !chapterId) return;
    const minutes = (Date.now() - this.chapterStartTime()) / 60000;
    if (!force && minutes < 0.1) return;
    // 关键修复：原来硬编码 progress: 5，导致后端 LearningProgress.Progress 全部为 5，
    // 拉低 StudentCourse.Progress 平均值 → 用户的"我的课程"列表里所有课程都显示 5%。
    // 改为使用 courseProgress()（基于已完成习题数/总习题数计算），与前端 UI 展示的进度一致。
    this.learningService.recordProgress({
      courseId: course.id,
      chapterId,
      progress: this.courseProgress(),
      additionalMinutes: Math.round(minutes),
    } as any).subscribe({
      next: () => {
        if (force) {
          this.chapterStartTime.set(Date.now());
        }
      },
      error: () => {},
    });
  }

  loadChapterContent(chapterId: string) {
    this.resourcesLoading.set(true);
    this.exercisesLoading.set(true);
    this.currentResources.set([]);
    this.currentExercises.set([]);
    this.currentExercise.set(null);
    this.submittedRecord.set(null);
    this.hasViewedAnswer.set(false);
    this.currentAnswer.set('');
    this.multiSelected.set(new Set());

    const chapter = this.findChapter(this.chapters(), chapterId);
    const resources = (chapter?.knowledgeResources || []) as KnowledgeResourceDto[];
    this.currentResources.set(resources);
    this.resourcesLoading.set(false);

    // 拉取章节习题
    this.exerciseService.getByChapter(chapterId).subscribe({
      next: (data: any) => {
        const list = (data?.items || data || []) as ExerciseDto[];
        this.currentExercises.set(list);
        this.exercisesLoading.set(false);
      },
      error: () => {
        this.exercisesLoading.set(false);
      },
    });
  }

  loadChapterRecords(chapterId: string) {
    const course = this.course();
    // 课程详情未就绪时直接跳过，避免 recordsLoading 卡在 true 导致永久等待图标；
    // 课程就绪后会由 loadCourse 补加载记录。
    if (!course?.id) return;
    this.recordsLoading.set(true);
    this.recordService.getRecordsByChapter({
      courseId: course.id,
      chapterId,
      skipCount: 0,
      maxResultCount: 50,
    } as any).subscribe({
      next: result => {
        const items = (result?.items || []) as StudentExerciseRecordDto[];
        this.chapterRecords.set(items);
        this.recordsLoading.set(false);
      },
      error: () => {
        this.recordsLoading.set(false);
      },
    });
  }

  private loadAllExercises(courseId: string, flat: FlatChapter[]) {
    this.exerciseService.getByCourse(courseId).subscribe({
      next: (data: any) => {
        const list = (data?.items || data || []) as ExerciseDto[];
        this.totalExercises.set(list.length);

        // 按章节分组统计习题总数
        const chapterTotalMap = new Map<string, number>();
        for (const ex of list) {
          const chId = ex.chapterId;
          if (chId) chapterTotalMap.set(chId, (chapterTotalMap.get(chId) || 0) + 1);
        }

        // 拉取课程下所有提交记录
        this.recordService.getRecordsByCourse({
          courseId,
          skipCount: 0,
          maxResultCount: 10000,
        } as any).subscribe({
          next: (recordResult: any) => {
            const records = (recordResult?.items || []) as StudentExerciseRecordDto[];
            const completedIds = new Set<string>(
              records.filter(r => r.exerciseId).map(r => r.exerciseId!)
            );
            this.completedCount.set(completedIds.size);

            // 按章节统计已提交习题数
            const chapterCompletedMap = new Map<string, number>();
            for (const ex of list) {
              const chId = ex.chapterId;
              if (chId && completedIds.has(ex.id!)) {
                chapterCompletedMap.set(chId, (chapterCompletedMap.get(chId) || 0) + 1);
              }
            }

            // 合并为章节进度 map
            const progressMap = new Map<string, { total: number; completed: number }>();
            for (const [chId, total] of chapterTotalMap) {
              progressMap.set(chId, {
                total,
                completed: chapterCompletedMap.get(chId) || 0,
              });
            }
            this.chapterProgressMap.set(progressMap);
          },
        });
      },
      error: () => {},
    });
  }

  // === Tab 切换 ===
  setTab(tab: TabKey) {
    this.activeTab.set(tab);
  }

  previewResource(r: KnowledgeResourceDto) {
    if (!r.resourceId) return;
    const course = this.course();
    const chapter = this.currentChapter();
    // 使用共享的文件预览组件
    this.filePreview.open(
      r.resourceId,
      r.originalFileName || r.name || '预览',
      r.fileExtension || '',
      r.fileSize || 0
    );
    // 记录学习进度
    if (course?.id) {
      this.learningService.recordProgress({
        courseId: course.id,
        chapterId: chapter?.id,
        resourceId: r.resourceId,
        progress: this.courseProgress(),
        additionalMinutes: 1,
      } as any).subscribe();
      this.message.success('已记录学习数据');
    }
  }

  downloadResource(r: KnowledgeResourceDto) {
    if (!r.resourceId) return;
    // 与资源界面保持一致：直接用 <a href> 触发浏览器原生下载，
    // 由浏览器接管下载进度条 / 取消 / 断点续传。
    // 之前用 RestService 取 blob 再 createObjectURL 的做法：
    // 1) 整个文件先进入 JS 内存，大文件卡死；
    // 2) 下载完成前浏览器无任何提示，也无法取消。
    const url = `/api/resource-file/${r.resourceId}/download`;
    const a = document.createElement('a');
    a.href = url;
    // 只在前端能拼出带扩展名的文件名时才覆盖，否则交给服务器 Content-Disposition
    const downloadName = buildDownloadFileName(r.originalFileName, r.name, r.fileExtension);
    if (downloadName) a.download = downloadName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    this.message.success('下载已开始');
    // 记录学习进度（下载已触发即记录，无需等待完成）
    const course = this.course();
    const chapter = this.currentChapter();
    if (course?.id) {
      this.learningService.recordProgress({
        courseId: course.id,
        chapterId: chapter?.id,
        resourceId: r.resourceId,
        progress: this.courseProgress(),
        additionalMinutes: 2,
      } as any).subscribe();
    }
  }

  // 分页辅助
  readonly pagedResources = computed(() => {
    const page = this.resourcePage();
    const size = this.resourcePageSize();
    const list = this.currentResources();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  onResourcePageChange(page: number) {
    this.resourcePage.set(page);
  }

  // === 提交记录：搜索 + 对错筛选 ===
  readonly recordKeyword = signal('');
  readonly recordResultFilter = signal<'all' | 'correct' | 'wrong' | 'pending'>('all');
  readonly filteredRecords = computed(() => {
    const kw = this.recordKeyword().trim().toLowerCase();
    const f = this.recordResultFilter();
    return this.chapterRecords().filter(r => {
      if (f === 'correct' && r.isCorrect !== true) return false;
      if (f === 'wrong' && r.isCorrect !== false) return false;
      if (f === 'pending' && r.isCorrect != null) return false;
      if (kw) {
        const hay = `${r.exerciseTitle || ''} ${r.studentAnswer || ''}`.toLowerCase();
        if (!hay.includes(kw)) return false;
      }
      return true;
    });
  });

  clearRecordFilter(): void {
    this.recordKeyword.set('');
    this.recordResultFilter.set('all');
  }

  /** 下拉筛选值回写（nz-select 输出 string，需收窄为联合类型） */
  setRecordResultFilter(v: string): void {
    if (v === 'correct' || v === 'wrong' || v === 'pending') {
      this.recordResultFilter.set(v);
    } else {
      this.recordResultFilter.set('all');
    }
  }

  /** 提交记录点击习题标题：跳到习题 Tab 并打开该题作答/查看 */
  jumpToExercise(record: StudentExerciseRecordDto): void {
    if (!record.exerciseId) return;
    const target = this.currentExercises().find(e => e.id === record.exerciseId);
    if (!target) {
      this.message.warning('该习题不在当前章节');
      return;
    }
    this.setTab('exercises');
    this.selectExercise(target);
  }

  // === 表格辅助 ===
  /** 已作答判断：提交记录中存在该习题即视为已作答 */
  isExerciseAnswered(exerciseId?: string | null): boolean {
    if (!exerciseId) return false;
    return this.answeredExerciseIds().has(exerciseId);
  }

  /** 纯文本资源行内正文的展开状态（无关联文件时行内展示正文） */
  private readonly expandedTextKeys = signal<Set<string>>(new Set());
  textRowKey(r: KnowledgeResourceDto, index: number): string {
    return r.id || `${r.name || 'row'}-${index}`;
  }
  isTextExpanded(key: string): boolean {
    return this.expandedTextKeys().has(key);
  }
  toggleTextExpand(key: string): void {
    const set = new Set(this.expandedTextKeys());
    if (set.has(key)) set.delete(key);
    else set.add(key);
    this.expandedTextKeys.set(set);
  }

  /** 文件大小格式化 */
  formatFileSize(bytes?: number | null): string {
    if (!bytes || bytes <= 0) return '—';
    if (bytes < 1024) return `${bytes} B`;
    const kb = bytes / 1024;
    if (kb < 1024) return `${kb.toFixed(kb < 10 ? 1 : 0)} KB`;
    const mb = kb / 1024;
    if (mb < 1024) return `${mb.toFixed(mb < 10 ? 1 : 0)} MB`;
    return `${(mb / 1024).toFixed(2)} GB`;
  }

  // === 习题 ===
  selectExercise(e: ExerciseDto) {
    this.currentExercise.set(e);
    this.submittedRecord.set(null);
    this.hasViewedAnswer.set(false);
    this.currentAnswer.set('');
    this.multiSelected.set(new Set());
    this.selfAssessment.set(SelfAssessment.None);
    this.exerciseStartTime.set(Date.now());
    // 查找已存在的记录
    const record = this.chapterRecords().find(r => r.exerciseId === e.id);
    if (record) {
      this.submittedRecord.set(record);
      this.hasViewedAnswer.set(record.hasViewedAnswer);
      this.currentAnswer.set(record.studentAnswer || '');
      this.selfAssessment.set(record.selfAssessment || SelfAssessment.None);
    }
  }

  setMultiOption(key: string, checked: boolean) {
    const set = new Set(this.multiSelected());
    if (checked) set.add(key);
    else set.delete(key);
    this.multiSelected.set(set);
  }

  isMultiSelected(key: string): boolean {
    return this.multiSelected().has(key);
  }

  /** 将存储的答案转为字母显示（1→A, 2→B, ...），兼容已有字母格式 */
  readonly letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';

  displayAnswer(raw: string | undefined | null, type?: ExerciseType): string {
    if (!raw) return '';
    // 判断题统一显示为中文，避免 true/TRUE/对 等多种写法让用户困惑
    const t = raw.trim().toLowerCase();
    if (type === ExerciseType.TrueFalse || t === 'true' || t === 'false') {
      if (['true', 't', '1', '对', '正确', '是', '√', '✓'].includes(t)) return '正确';
      if (['false', 'f', '0', '错', '错误', '否', '×', 'x'].includes(t)) return '错误';
    }
    const tokens = raw.split(/[,;，；、\s|/]+/).map(s => s.trim()).filter(Boolean);
    // 兼容无分隔符连写（如历史数据 "ABC"）：拆成单个字母
    const expanded: string[] = [];
    for (const tok of tokens) {
      if (/^[A-Za-z]{2,6}$/.test(tok) && !/^(true|false)$/i.test(tok)) {
        expanded.push(...tok.toUpperCase().split(''));
      } else {
        expanded.push(tok);
      }
    }
    return expanded.map(tok => {
      if (/^\d+$/.test(tok)) {
        const i = Number(tok);
        if (i === 0) return 'A';
        if (i >= 1 && i <= 26) return this.letters[i - 1];
        return tok;
      }
      return tok.toUpperCase();
    }).join(',');
  }

  parseOptions(optionsStr?: string | null): OptionItem[] {
    if (!optionsStr) return [];
    const cached = this.optionsCache.get(optionsStr);
    if (cached) return cached;

    let result: OptionItem[] = [];
    try {
      const parsed = JSON.parse(optionsStr);
      if (Array.isArray(parsed) && parsed.length > 0) {
        result = parsed.map((o: any, idx: number) => {
          // 对象格式: {key: "A", content: "..."}
          if (typeof o === 'object' && o !== null) {
            return {
              key: o.key || o.Key || String.fromCharCode(65 + idx),
              content: o.content || o.Content || '',
            };
          }
          // 字符串数组格式: ["选项A", "选项B", ...]
          return {
            key: String.fromCharCode(65 + idx),
            content: String(o),
          };
        });
      }
    } catch {
      // 不是 JSON，尝试按换行分割
      result = optionsStr
        .split(/\r?\n/)
        .map(line => line.trim())
        .filter(line => line.length > 0)
        .map((line, idx) => ({
          key: String.fromCharCode(65 + idx),
          content: line.replace(/^[A-Z][\.\)]\s*/, ''),
        }));
    }

    // 简单防膨胀：缓存条目过多时整体清空
    if (this.optionsCache.size > 200) this.optionsCache.clear();
    this.optionsCache.set(optionsStr, result);
    return result;
  }

  getCurrentAnswerText(): string {
    const ex = this.currentExercise();
    if (!ex) return '';
    if (ex.type === ExerciseType.MultiChoice) {
      return Array.from(this.multiSelected()).sort().join(',');
    }
    return this.currentAnswer();
  }

  submitAnswer() {
    const ex = this.currentExercise();
    const course = this.course();
    const chapter = this.currentChapter();
    if (!ex || !course || !chapter) return;
    if (this.submitting()) return;

    const answer = this.getCurrentAnswerText();
    if (!answer.trim()) {
      this.message.warning('请先作答');
      return;
    }

    this.submitting.set(true);
    this.learningService.recordProgress({
      courseId: course.id,
      chapterId: chapter.id,
      progress: this.courseProgress(),
      additionalMinutes: 1,
    } as any).subscribe();

    const elapsed = Date.now() - this.exerciseStartTime();
    // timeSpentTicks = ms * 10000, 与 exercise-learning.component 保持一致
    const timeSpentTicks = Math.max(elapsed, 1000) * 10000;
    this.recordService.saveOrUpdateRecord({
      courseId: course.id,
      chapterId: chapter.id,
      exerciseId: ex.id,
      studentAnswer: answer,
      timeSpentTicks,
    } as any).subscribe({
      next: (record: any) => {
        this.submitting.set(false);
        this.submittedRecord.set(record);
        // 更新进度计数
        if (this.chapterRecords().find(r => r.exerciseId === ex.id)) {
          // 已有记录（重提交），不增加计数
        } else {
          this.completedCount.update(c => c + 1);
        }
        this.message.success('提交成功');
        this.loadChapterRecords(chapter.id!);
      },
      error: () => {
        this.submitting.set(false);
        this.message.error('提交失败');
      },
    });
  }

  viewAnswer() {
    const ex = this.currentExercise();
    const course = this.course();
    if (!ex || !course) return;
    this.recordService.markAnswerViewed({ courseId: course.id, exerciseId: ex.id } as any).subscribe({
      next: () => {
        this.hasViewedAnswer.set(true);
        this.message.info('已记录查看答案');
      },
    });
  }

  setSelfAssessment(value: SelfAssessment) {
    const ex = this.currentExercise();
    const course = this.course();
    if (!ex || !course) return;
    this.selfAssessment.set(value);
    this.recordService.submitSelfAssessment({ courseId: course.id, exerciseId: ex.id, assessment: value } as any).subscribe({
      next: () => this.message.success('已记录自评'),
    });
  }

  isCorrect(record: StudentExerciseRecordDto | null): boolean | null {
    if (!record) return null;
    return record.isCorrect ?? null;
  }

  // === Tree control ===
  isExpanded(id: string): boolean {
    return this.expandedNodes().has(id);
  }

  toggleNode(id: string) {
    const set = new Set(this.expandedNodes());
    if (set.has(id)) set.delete(id);
    else set.add(id);
    this.expandedNodes.set(set);
  }

  goBack() {
    const course = this.course();
    if (course?.id) {
      // 保留 fromMicroMajor 等来源参数，回到课程详情后“返回”仍能回到来源页
      this.router.navigate(['/student/courses', course.id], { queryParamsHandling: 'preserve' });
    } else {
      this.router.navigate(['/student/courses']);
    }
  }

  difficultyLabel(d?: number): string {
    return DIFFICULTY_LABELS[(d || 1) - 1] || '未设置';
  }

  importanceLabel(level?: string): string {
    return IMPORTANCE_LABELS[level || 'normal'] || '一般';
  }

  importanceColor(level?: string): string {
    return IMPORTANCE_COLORS[level || 'normal'] || '#2b6cd4';
  }

  exerciseTypeLabel(t?: ExerciseType): string {
    return EXERCISE_TYPE_LABELS[t || 0] || '未知';
  }

  trackResource = (_: number, r: KnowledgeResourceDto) => r.id;
  trackExercise = (_: number, e: ExerciseDto) => e.id;
  trackRecord = (_: number, r: StudentExerciseRecordDto) => r.id;
}
