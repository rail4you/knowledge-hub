import { Component, signal, inject, OnInit, OnDestroy, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { MyCoursesService } from '../../learning/shared/my-courses.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { ChapterPickerComponent } from '../../shared/chapter-picker/chapter-picker.component';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import type { CourseDto, ChapterDto } from '../../proxy/courses/dtos/models';
import type { CreateUpdateExerciseDto, ExerciseDto } from '../../proxy/exams/dtos/models';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';
import { firstValueFrom, Subject, Subscription } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AiGenerationTaskDto, AiTaskService, AiTaskStatus, AiTaskType } from '../services/ai-task.service';
import { AiTaskNotificationService } from '../services/ai-task-notification.service';

@Component({
  selector: 'app-exercise-generate',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzTagModule,
    NzIconModule,
    NzSpinModule,
    NzProgressModule,
    NzTableModule,
    NzModalModule,
    NzFormModule,
    NzEmptyModule,
    NzTooltipModule,
    NzTabsModule,
    NzCheckboxModule,    NzRadioModule,
    ChapterPickerComponent,
  ],
  templateUrl: './exercise-generate.component.html',
  styleUrls: ['./exercise-generate.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExerciseGenerateComponent implements OnInit, OnDestroy {
  /** 课程下拉数据源：教师只列自己创建的课程（与「课程列表」同一口径） */
  private readonly myCourses = inject(MyCoursesService);
  private readonly chapterService = inject(ChapterService);
  private readonly exerciseService = inject(ExerciseService);
  private readonly message = inject(NzMessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly aiTaskService = inject(AiTaskService);
  private readonly aiTaskNotifications = inject(AiTaskNotificationService);
  private readonly destroy$ = new Subject<void>();
  private taskPollSub: Subscription | null = null;
  private lastPreviewTaskId: string | null = null;

  readonly courses = signal<CourseDto[]>([]);
  readonly courseId = signal<string | null>(null);
  readonly chapterTree = signal<ChapterDto[]>([]);
  readonly selectedChapterIds = signal<string[]>([]);
  readonly exerciseType = signal<ExerciseType>(ExerciseType.SingleChoice);
  readonly difficulty = signal<number>(2);
  readonly count = signal<number>(1);
  readonly topicHint = signal('');
  readonly customPrompt = signal('');

  /** 确认框提交中（仅提交请求的短暂时长，提交成功后立即关闭弹窗） */
  readonly submitting = signal(false);
  /** 后台任务运行中（驱动结果 Tab 的进度展示） */
  readonly taskRunning = signal(false);
  readonly taskProgress = signal(0);
  readonly taskMessage = signal('');
  readonly results = signal<ExerciseDto[]>([]);
  readonly activeTab = signal(0);

  // ── 结果表格：搜索 / 筛选（客户端） ──
  readonly resultKeyword = signal('');
  readonly resultTypeFilter = signal<ExerciseType | null>(null);
  readonly resultDifficultyFilter = signal<number | null>(null);
  readonly resultSourceFilter = signal<'all' | 'ai' | 'manual'>('all');

  readonly filteredResults = computed(() => {
    const kw = this.resultKeyword().trim().toLowerCase();
    const type = this.resultTypeFilter();
    const difficulty = this.resultDifficultyFilter();
    const source = this.resultSourceFilter();
    return this.results().filter((e) => {
      if (type !== null && e.type !== type) return false;
      if (difficulty !== null && e.difficulty !== difficulty) return false;
      if (source === 'ai' && !e.isAiGenerated) return false;
      if (source === 'manual' && e.isAiGenerated) return false;
      if (kw) {
        const hit =
          (e.title || '').toLowerCase().includes(kw) ||
          (e.questionContent || '').toLowerCase().includes(kw) ||
          (e.answer || '').toLowerCase().includes(kw);
        if (!hit) return false;
      }
      return true;
    });
  });

  // 显式分页（不依赖 nz-table 的前端分页 data，避免 OnPush 下首次渲染取不到数据）
  readonly resultPageIndex = signal(1);
  readonly resultPageSize = signal(10);
  readonly pagedResults = computed(() => {
    const start = (this.resultPageIndex() - 1) * this.resultPageSize();
    return this.filteredResults().slice(start, start + this.resultPageSize());
  });

  readonly hasResultFilter = computed(
    () =>
      this.resultKeyword().trim() !== '' ||
      this.resultTypeFilter() !== null ||
      this.resultDifficultyFilter() !== null ||
      this.resultSourceFilter() !== 'all',
  );

  clearResultFilter() {
    this.resultKeyword.set('');
    this.resultTypeFilter.set(null);
    this.resultDifficultyFilter.set(null);
    this.resultSourceFilter.set('all');
    this.resultPageIndex.set(1);
  }

  onResultPageIndexChange(page: number) {
    this.resultPageIndex.set(page);
  }

  onResultPageSizeChange(size: number) {
    this.resultPageSize.set(size);
    this.resultPageIndex.set(1);
  }

  // ── 生成前确认（核对本次输入，确认后才真正生成） ──
  readonly confirmVisible = signal(false);

  readonly selectedCourseTitle = computed(() => {
    const id = this.courseId();
    return this.courses().find(c => c.id === id)?.title || '';
  });

  /** 已选章节标题（含完整路径，如：第一章 / 1.2 概述），按章节树顺序排列 */
  readonly selectedChapterTitles = computed(() => {
    const ids = new Set(this.selectedChapterIds());
    if (ids.size === 0) return [];
    const out: string[] = [];
    const walk = (nodes: ChapterDto[] | undefined, ancestors: string[]) => {
      for (const n of nodes || []) {
        const title = n.title || '未命名章节';
        const path = [...ancestors, title].join(' / ');
        if (n.id && ids.has(n.id)) out.push(path);
        walk(n.children, [...ancestors, title]);
      }
    };
    walk(this.chapterTree(), []);
    return out;
  });

  openConfirm() {
    if (!this.courseId()) {
      this.message.warning('请先选择课程');
      return;
    }
    this.confirmVisible.set(true);
  }

  closeConfirm() {
    if (this.submitting()) return;
    this.confirmVisible.set(false);
  }

  readonly resultTabTitle = computed(() =>
    this.results().length > 0 ? `生成结果（${this.results().length}）` : '生成结果'
  );

  // ── 章节选择：UI 由共享组件 ChapterPickerComponent 负责（多选 + 嵌套表格） ──
  readonly chapterLoading = signal(false);

  // ── 编辑弹窗（AI 生成的习题可直接编辑） ──
  readonly editVisible = signal(false);
  readonly editSaving = signal(false);
  editing: ExerciseDto | null = null;
  editTitle = '';
  editQuestionContent = '';
  editOptions: string[] = [];
  editAnswer = '';
  /** 判断题答案单选项：'true' / 'false' */
  editTrueFalse: 'true' | 'false' | '' = '';
  editQuestionAnalysis = '';
  editDifficulty = 2;
  editScore = 1;

  readonly letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';

  /**
   * 返回「习题管理」：本页是习题管理的深链页（只有 AI 任务通知 / 任务中心会跳进来），
   * 必须给一个明确的退出口；带上 courseId 让目标页自动选中同一门课。
   */
  backToManage(): void {
    const courseId = this.courseId() || this.route.snapshot.queryParamMap.get('courseId');
    void this.router.navigate(
      ['/learning/exercise-management'],
      courseId ? { queryParams: { courseId } } : undefined,
    );
  }

  ngOnInit() {
    this.loadCourses();
    const presetCourseId = this.route.snapshot.queryParamMap.get('courseId');
    // ?chapterId= 来自「章节习题」页：按章节生成时预选该章节
    const presetChapterId = this.route.snapshot.queryParamMap.get('chapterId');
    if (presetCourseId) {
      this.courseId.set(presetCourseId);
      this.loadChapterTree(presetChapterId);
    }
    const taskId = this.route.snapshot.queryParamMap.get('taskId');
    if (taskId) {
      this.lastPreviewTaskId = taskId;
      this.loadTaskPreview(taskId);
    } else {
      // 从任务中心返回 / 切页回来时，恢复最近一次习题生成任务的状态与结果
      this.loadLatestTask();
    }
    // ?taskId= 深度链接（通知 / 任务中心跳转）：响应式订阅，页内跳转同样生效
    this.route.queryParamMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const id = params.get('taskId');
        if (id && id !== this.lastPreviewTaskId) {
          this.lastPreviewTaskId = id;
          this.loadTaskPreview(id);
        }
      });
  }

  ngOnDestroy(): void {
    this.cancelTaskPolling();
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadCourses() {
    // 教师只列自己创建的课程（院校 / 联盟 / 平台管理员看本校全部）—— 口径由 MyCoursesService 统一维护。
    this.myCourses.getCourses(200).subscribe({
      next: (items) => this.courses.set(items || []),
    });
  }

  onCourseChange(courseId: string | null) {
    this.courseId.set(courseId);
    this.selectedChapterIds.set([]);
    this.chapterTree.set([]);
    if (courseId) this.loadChapterTree();
  }

  /**
   * 加载章节树。
   * @param selectChapterId 需要预选的章节（来自 ?chapterId=，按章节生成场景）
   */
  loadChapterTree(selectChapterId?: string | null) {
    const courseId = this.courseId();
    if (!courseId) return;
    this.chapterLoading.set(true);
    this.chapterService.getChapterTree(courseId).subscribe({
      next: data => {
        const list = data || [];
        this.chapterTree.set(list);
        this.chapterLoading.set(false);
        if (selectChapterId) {
          this.preselectChapter(selectChapterId);
        }
      },
      error: () => {
        this.chapterLoading.set(false);
        this.message.error('加载章节失败');
      },
    });
  }

  /**
   * 按 id 预选章节（章节树里存在才生效），并展开到该章节路径。
   * 场景：从「章节习题」页点 AI 生成 → /ai/exercise-generate?courseId=x&chapterId=y
   */
  private preselectChapter(chapterId: string) {
    const exists = (nodes: ChapterDto[] | undefined): boolean => {
      for (const n of nodes || []) {
        if (n.id === chapterId) return true;
        if (exists(n.children)) return true;
      }
      return false;
    };
    if (!exists(this.chapterTree())) return;

    // 章节树默认「展开全部」，这里只需要回填选中项
    this.selectedChapterIds.set([chapterId]);
  }

  /** 确认框中点「确认生成」后提交后台任务（完成后自动保存入库） */
  generate() {
    const courseId = this.courseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    const chapterIds = this.selectedChapterIds();
    const payload = {
      courseId,
      chapterId: chapterIds.length > 0 ? chapterIds[0] : undefined,
      chapterIds,
      type: this.exerciseType(),
      count: this.count(),
      difficulty: this.difficulty(),
      topicHint: this.topicHint().trim() || undefined,
      customPrompt: this.customPrompt().trim() || undefined,
    };

    this.submitting.set(true);
    this.cancelTaskPolling();
    this.aiTaskService
      .create({
        taskType: AiTaskType.ExerciseGenerate,
        title: this.selectedCourseTitle() ? `习题生成：${this.selectedCourseTitle()}` : '习题生成',
        resourceId: undefined,
        resourceName: this.selectedCourseTitle() || undefined,
        inputJson: JSON.stringify(payload),
      })
      .subscribe({
        next: (task) => {
          // 提交成功后立即关闭确认框，任务转入后台执行，不再阻塞页面
          this.submitting.set(false);
          this.confirmVisible.set(false);
          this.taskRunning.set(true);
          this.taskProgress.set(0);
          this.taskMessage.set('任务已提交，正在后台生成…');
          this.activeTab.set(1);
          this.message.success('任务已提交后台生成，可切换页面，完成后会通知你');
          this.followTask(task.id);
        },
        error: (e) => {
          this.submitting.set(false);
          this.message.error(e?.error?.error?.message || '提交任务失败，请重试');
        },
      });
  }

  // ---------- background task helpers ----------
  private followTask(taskId: string) {
    this.cancelTaskPolling();
    this.taskPollSub = this.aiTaskNotifications
      .pollTask(taskId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (t) => {
          if (t.status === AiTaskStatus.Pending || t.status === AiTaskStatus.Running) {
            this.taskRunning.set(true);
            this.taskProgress.set(t.progress ?? 0);
            this.taskMessage.set(t.progressMessage || '生成中…');
            return;
          }

          this.cancelTaskPolling();
          this.taskRunning.set(false);

          if (t.status === AiTaskStatus.Completed) {
            const exercises = this.parseExercises(t.resultJson);
            this.results.set(exercises);
            this.activeTab.set(1);
            this.message.success(`AI 已生成并保存 ${exercises.length} 道习题`);
          } else {
            this.message.error(t.errorMessage || 'AI 生成失败，请重试');
          }
        },
        error: () => {
          this.cancelTaskPolling();
          this.taskRunning.set(false);
          this.message.error('AI 生成失败，请重试');
        },
      });
  }

  private cancelTaskPolling() {
    this.taskPollSub?.unsubscribe();
    this.taskPollSub = null;
  }

  private loadTaskPreview(taskId: string) {
    this.aiTaskService
      .get(taskId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (task) => this.handleTask(task, true),
        error: () => this.message.error('加载任务结果失败'),
      });
  }

  /** 页面无 taskId 时，恢复当前用户最近一次习题生成任务（不切换 Tab，保持停留在「生成习题」） */
  private loadLatestTask() {
    this.aiTaskService
      .getList({ taskType: AiTaskType.ExerciseGenerate, onlyMine: true, maxResultCount: 1 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          const task = res.items?.[0];
          if (!task) return;
          // 列表接口不返回 ResultJson（大字段），需再取详情才能拿到结果
          this.aiTaskService
            .get(task.id)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
              next: (full) => this.handleTask(full, false),
              error: () => this.handleTask(task, false),
            });
        },
      });
  }

  private handleTask(task: AiGenerationTaskDto, switchTab: boolean) {
    if (task.status === AiTaskStatus.Completed) {
      this.results.set(this.parseExercises(task.resultJson));
      if (switchTab) this.activeTab.set(1);
    } else if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
      this.taskRunning.set(true);
      this.taskProgress.set(task.progress ?? 0);
      this.taskMessage.set(task.progressMessage || '后台生成中…');
      if (switchTab) this.activeTab.set(1);
      this.followTask(task.id);
    }
  }

  private parseExercises(raw?: string | null): ExerciseDto[] {
    if (!raw) return [];
    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? (parsed as ExerciseDto[]) : [];
    } catch {
      return [];
    }
  }

  /** 单题「去章节关联」：定位到该题所属课程 + 主章节 */
  goChapterExerciseFor(e: ExerciseDto) {
    const courseId = e.courseId || this.courseId();
    const chapterId = e.chapterId || e.chapterIds?.[0] || this.selectedChapterIds()[0] || null;
    const queryParams: Record<string, string> = {};
    if (courseId) queryParams['courseId'] = courseId;
    if (chapterId) queryParams['chapterId'] = chapterId;
    this.router.navigate(['/learning/chapter-exercise'], {
      queryParams: Object.keys(queryParams).length > 0 ? queryParams : undefined,
    });
  }

  // ── 结果展示 ──
  parseOptions(e: ExerciseDto): string[] {
    if (!e.options) return [];
    try {
      const arr = JSON.parse(e.options);
      return Array.isArray(arr) ? arr.map(x => String(x)) : [];
    } catch {
      return [];
    }
  }

  isChoice(e: ExerciseDto): boolean {
    return e.type === ExerciseType.SingleChoice || e.type === ExerciseType.MultiChoice;
  }

  /** 判断题（编辑弹窗用：正确/错误 单选项） */
  isTrueFalse(e: ExerciseDto | null): boolean {
    return e?.type === ExerciseType.TrueFalse;
  }

  /** 结果表答案展示：判断题显示 正确/错误，选择题显示字母，其余显示文本（截断） */
  displayAnswer(e: ExerciseDto): string {
    const raw = (e.answer ?? '').trim();
    if (!raw) return '-';

    if (e.type === ExerciseType.TrueFalse) {
      const t = raw.toLowerCase().replace(/[。.!！'"\s]/g, '');
      if (['true', 't', '1', 'yes', 'y', '√', '✓', '对', '正确', '是', '真'].includes(t)) return '正确';
      if (['false', 'f', '0', 'no', 'n', '×', 'x', '错', '错误', '否', '假'].includes(t)) return '错误';
      return raw;
    }

    if (this.isChoice(e)) return raw.toUpperCase();
    return raw.length > 20 ? raw.substring(0, 20) + '…' : raw;
  }

  // ── 编辑 / 删除 ──
  openEdit(e: ExerciseDto) {
    this.editing = e;
    this.editTitle = e.title || '';
    this.editQuestionContent = e.questionContent || '';
    this.editOptions = this.parseOptions(e);
    if (this.isChoice(e) && this.editOptions.length < 4) {
      while (this.editOptions.length < 4) this.editOptions.push('');
    }
    this.editAnswer = e.answer || '';
    // 判断题：把已存答案归一化为 true/false 供正确/错误单选项回显
    if (this.isTrueFalse(e)) {
      const t = (e.answer ?? '').trim().toLowerCase().replace(/[。.!！'"\s]/g, '');
      this.editTrueFalse = ['true', 't', '1', 'yes', 'y', '√', '✓', '对', '正确', '是', '真'].includes(t)
        ? 'true'
        : ['false', 'f', '0', 'no', 'n', '×', 'x', '错', '错误', '否', '假'].includes(t)
          ? 'false'
          : '';
    } else {
      this.editTrueFalse = '';
    }
    this.editQuestionAnalysis = e.questionAnalysis || '';
    this.editDifficulty = e.difficulty ?? 2;
    this.editScore = e.score ?? 1;
    this.editVisible.set(true);
  }

  addEditOption() {
    if (this.editOptions.length >= 8) return;
    this.editOptions = [...this.editOptions, ''];
  }

  removeEditOption(index: number) {
    this.editOptions = this.editOptions.filter((_, i) => i !== index);
  }

  updateEditOption(index: number, value: string) {
    this.editOptions = this.editOptions.map((opt, i) => (i === index ? value : opt));
  }

  closeEdit() {
    this.editVisible.set(false);
    this.editing = null;
  }

  async saveEdit() {
    if (!this.editing?.id) return;
    if (!this.editTitle.trim() || !this.editQuestionContent.trim()) {
      this.message.warning('请填写标题和题目内容');
      return;
    }
    const dto: CreateUpdateExerciseDto = {
      courseId: this.editing.courseId,
      chapterId: this.editing.chapterId,
      chapterIds: this.editing.chapterIds || [],
      title: this.editTitle.trim(),
      questionContent: this.editQuestionContent.trim(),
      type: this.editing.type,
      options: this.isChoice(this.editing)
        ? JSON.stringify(
            this.editOptions.map(s => (s ?? '').trim()).filter(s => s.length > 0)
          )
        : this.editing.options,
      answer: this.isTrueFalse(this.editing)
        ? (this.editTrueFalse || this.editAnswer.trim())
        : this.editAnswer.trim(),
      questionAnalysis: this.editQuestionAnalysis.trim() || undefined,
      difficulty: this.editDifficulty,
      score: this.editScore,
    };
    this.editSaving.set(true);
    try {
      const updated = await firstValueFrom(this.exerciseService.update(this.editing.id, dto));
      this.results.set(this.results().map(r => (r.id === updated.id ? updated : r)));
      this.message.success('已保存修改');
      this.closeEdit();
    } catch {
      this.message.error('保存失败');
    } finally {
      this.editSaving.set(false);
    }
  }

  async deleteOne(e: ExerciseDto) {
    if (!e.id) return;
    try {
      await firstValueFrom(this.exerciseService.delete(e.id));
      this.results.set(this.results().filter(r => r.id !== e.id));
      this.message.success('已删除');
    } catch {
      this.message.error('删除失败');
    }
  }

  // ── 展示 helpers ──
  getTypeName(type: ExerciseType | undefined): string {
    const names: Record<number, string> = {
      [ExerciseType.SingleChoice]: '单选题',
      [ExerciseType.MultiChoice]: '多选题',
      [ExerciseType.TrueFalse]: '判断题',
      [ExerciseType.FillBlank]: '填空题',
      [ExerciseType.ShortAnswer]: '问答题',
      [ExerciseType.Essay]: '论述题',
      [ExerciseType.CaseAnalysis]: '案例分析',
      [ExerciseType.TermExplanation]: '名词解释',
      [ExerciseType.Calculation]: '计算题',
      [ExerciseType.Programming]: '编程题',
    };
    return type === undefined ? '未知' : (names[type] ?? '未知');
  }

  getTypeColor(type: ExerciseType | undefined): string {
    const colors: Record<number, string> = {
      [ExerciseType.SingleChoice]: 'blue',
      [ExerciseType.MultiChoice]: 'purple',
      [ExerciseType.TrueFalse]: 'cyan',
      [ExerciseType.FillBlank]: 'orange',
      [ExerciseType.ShortAnswer]: 'gold',
      [ExerciseType.Essay]: 'red',
      [ExerciseType.CaseAnalysis]: 'green',
      [ExerciseType.TermExplanation]: 'geekblue',
      [ExerciseType.Calculation]: 'magenta',
      [ExerciseType.Programming]: 'lime',
    };
    return type === undefined ? 'default' : (colors[type] ?? 'default');
  }

  getDifficultyName(d: number | undefined): string {
    const names = ['', '入门', '简单', '中等', '困难', '专家'];
    return d ? (names[d] || '未知') : '未知';
  }

  trackResult = (_: number, e: ExerciseDto) => e.id;
}
