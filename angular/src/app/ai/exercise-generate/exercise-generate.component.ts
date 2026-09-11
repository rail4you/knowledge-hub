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
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
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
    NzCheckboxModule,
  ],
  templateUrl: './exercise-generate.component.html',
  styleUrls: ['./exercise-generate.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExerciseGenerateComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
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

  // ── 章节选择器（嵌套表格，参考章节管理） ──
  readonly chapterLoading = signal(false);
  readonly chapterKeyword = signal('');
  readonly expandedIds = signal<Set<string>>(new Set());

  /**
   * 展开层级（下拉列表控制）：
   * 0 = 仅一级，1 = 展开到二级，2 = 展开到三级，-1 = 展开全部
   */
  readonly expandLevel = signal<0 | 1 | 2 | -1>(-1);

  onExpandLevelChange(level: 0 | 1 | 2 | -1) {
    this.expandLevel.set(level);
    if (level === -1) this.expandAllChapters();
    else if (level === 0) this.collapseAllChapters();
    else this.expandToDepth(level);
  }

  /** 搜索过滤后的章节树（保留命中节点的祖先链） */
  readonly visibleChapters = computed(() => {
    const kw = this.chapterKeyword().trim().toLowerCase();
    const all = this.chapterTree();
    if (!kw) return all;
    const prune = (nodes: ChapterDto[]): ChapterDto[] => {
      const out: ChapterDto[] = [];
      for (const n of nodes || []) {
        const kids = n.children?.length ? prune(n.children) : [];
        const hitTitle = (n.title ?? '').toLowerCase().includes(kw);
        const hitDesc = (n.description ?? '').toLowerCase().includes(kw);
        if (hitTitle || hitDesc || kids.length > 0) {
          out.push({ ...n, children: kids });
        }
      }
      return out;
    };
    return prune(all);
  });

  readonly isChapterFiltering = computed(() => this.chapterKeyword().trim() !== '');

  /** 全部章节总数（含子章节） */
  readonly totalChapterCount = computed(() => this.countNodes(this.chapterTree()));

  private countNodes(nodes: ChapterDto[] | undefined): number {
    let count = 0;
    for (const n of nodes || []) {
      count++;
      count += this.countNodes(n.children);
    }
    return count;
  }

  private collectIds(nodes: ChapterDto[] | undefined, out: string[]) {
    for (const n of nodes || []) {
      if (n.id) out.push(n.id);
      this.collectIds(n.children, out);
    }
  }

  /** 当前可见章节 id（含子孙） */
  readonly visibleChapterIds = computed(() => {
    const ids: string[] = [];
    this.collectIds(this.visibleChapters(), ids);
    return ids;
  });

  readonly isAllVisibleChecked = computed(() => {
    const ids = this.visibleChapterIds();
    if (ids.length === 0) return false;
    const sel = new Set(this.selectedChapterIds());
    return ids.every(id => sel.has(id));
  });

  readonly isVisibleIndeterminate = computed(() => {
    const ids = this.visibleChapterIds();
    if (ids.length === 0) return false;
    const sel = new Set(this.selectedChapterIds());
    const some = ids.some(id => sel.has(id));
    return some && !ids.every(id => sel.has(id));
  });

  hasChildren(node: ChapterDto): boolean {
    return !!node.children && node.children.length > 0;
  }

  isExpanded(id: string | undefined): boolean {
    return !!id && this.expandedIds().has(id);
  }

  toggleExpand(id: string | undefined) {
    if (!id) return;
    const set = new Set(this.expandedIds());
    if (set.has(id)) set.delete(id);
    else set.add(id);
    this.expandedIds.set(set);
  }

  expandAllChapters() {
    const ids: string[] = [];
    const walk = (nodes: ChapterDto[] | undefined) => {
      for (const n of nodes || []) {
        if (this.hasChildren(n) && n.id) ids.push(n.id);
        walk(n.children);
      }
    };
    walk(this.chapterTree());
    this.expandedIds.set(new Set(ids));
  }

  collapseAllChapters() {
    this.expandedIds.set(new Set());
  }

  /**
   * 按层级展开：展开深度 < depth 的所有父节点。
   * depth=1 → 仅展开一级（看到一、二级）；depth=2 → 看到一、二、三级。
   */
  expandToDepth(depth: number) {
    const ids = new Set<string>();
    const walk = (nodes: ChapterDto[] | undefined, d: number) => {
      for (const n of nodes || []) {
        if (d < depth && this.hasChildren(n) && n.id) ids.add(n.id);
        walk(n.children, d + 1);
      }
    };
    walk(this.chapterTree(), 0);
    this.expandedIds.set(ids);
  }

  /** 某节点（含全部子孙）是否全选 */
  isChapterChecked(node: ChapterDto): boolean {
    const ids: string[] = [];
    this.collectIds([node], ids);
    if (ids.length === 0) return false;
    const sel = new Set(this.selectedChapterIds());
    return ids.every(id => sel.has(id));
  }

  /** 某节点是否部分选中 */
  isChapterIndeterminate(node: ChapterDto): boolean {
    const ids: string[] = [];
    this.collectIds([node], ids);
    if (ids.length === 0) return false;
    const sel = new Set(this.selectedChapterIds());
    const some = ids.some(id => sel.has(id));
    return some && !ids.every(id => sel.has(id));
  }

  toggleChapter(node: ChapterDto, checked: boolean) {
    const ids: string[] = [];
    this.collectIds([node], ids);
    const set = new Set(this.selectedChapterIds());
    if (checked) ids.forEach(id => set.add(id));
    else ids.forEach(id => set.delete(id));
    this.selectedChapterIds.set([...set]);
  }

  /** 表头勾选框：全选 / 取消全选当前可见章节 */
  toggleAllVisible(checked: boolean) {
    const set = new Set(this.selectedChapterIds());
    if (checked) this.visibleChapterIds().forEach(id => set.add(id));
    else this.visibleChapterIds().forEach(id => set.delete(id));
    this.selectedChapterIds.set([...set]);
  }

  // ── 编辑弹窗（AI 生成的习题可直接编辑） ──
  readonly editVisible = signal(false);
  readonly editSaving = signal(false);
  editing: ExerciseDto | null = null;
  editTitle = '';
  editQuestionContent = '';
  editOptions: string[] = [];
  editAnswer = '';
  editQuestionAnalysis = '';
  editDifficulty = 2;
  editScore = 1;

  readonly letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';

  ngOnInit() {
    this.loadCourses();
    const presetCourseId = this.route.snapshot.queryParamMap.get('courseId');
    if (presetCourseId) {
      this.courseId.set(presetCourseId);
      this.loadChapterTree();
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
    this.courseService.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe({
      next: result => this.courses.set(result.items || []),
    });
  }

  onCourseChange(courseId: string | null) {
    this.courseId.set(courseId);
    this.selectedChapterIds.set([]);
    this.chapterTree.set([]);
    this.expandedIds.set(new Set());
    this.expandLevel.set(-1);
    this.chapterKeyword.set('');
    if (courseId) this.loadChapterTree();
  }

  loadChapterTree() {
    const courseId = this.courseId();
    if (!courseId) return;
    this.chapterLoading.set(true);
    this.chapterService.getChapterTree(courseId).subscribe({
      next: data => {
        const list = data || [];
        this.chapterTree.set(list);
        // 默认展开全部
        this.expandLevel.set(-1);
        this.expandAllChapters();
        this.chapterLoading.set(false);
      },
      error: () => {
        this.chapterLoading.set(false);
        this.message.error('加载章节失败');
      },
    });
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
        next: (task) => this.handleTask(task),
        error: () => this.message.error('加载任务结果失败'),
      });
  }

  /** 页面无 taskId 时，恢复当前用户最近一次习题生成任务 */
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
              next: (full) => this.handleTask(full),
              error: () => this.handleTask(task),
            });
        },
      });
  }

  private handleTask(task: AiGenerationTaskDto) {
    if (task.status === AiTaskStatus.Completed) {
      this.results.set(this.parseExercises(task.resultJson));
      this.activeTab.set(1);
    } else if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
      this.taskRunning.set(true);
      this.taskProgress.set(task.progress ?? 0);
      this.taskMessage.set(task.progressMessage || '后台生成中…');
      this.activeTab.set(1);
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
      answer: this.editAnswer.trim(),
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
    };
    return type === undefined ? 'default' : (colors[type] ?? 'default');
  }

  getDifficultyName(d: number | undefined): string {
    const names = ['', '入门', '简单', '中等', '困难', '专家'];
    return d ? (names[d] || '未知') : '未知';
  }

  trackResult = (_: number, e: ExerciseDto) => e.id;
  trackChapter = (_: number, n: ChapterDto) => n.id;
}
