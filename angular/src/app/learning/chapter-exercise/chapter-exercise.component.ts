import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import type { CourseDto, ChapterDto } from '../../proxy/courses/dtos/models';
import type { CreateUpdateExerciseDto, ExerciseDto } from '../../proxy/exams/dtos/models';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';
import { Router } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-chapter-exercise',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzTagModule,
    NzIconModule,
    NzSpinModule,
    NzSelectModule,
    NzModalModule,
    NzTableModule,
    NzCheckboxModule,
    NzPaginationModule,
    NzTooltipModule,
    NzSwitchModule,
  ],
  templateUrl: './chapter-exercise.component.html',
  styleUrls: ['./chapter-exercise.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChapterExerciseComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);
  private readonly exerciseService = inject(ExerciseService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** 从其它页面跳转进来时待选中的课程 / 章节（等数据加载完成后再选） */
  private pendingCourseId: string | null = null;
  private pendingChapterId: string | null = null;

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly chapters = signal<ChapterDto[]>([]);
  readonly expandedNodes = signal<Set<string>>(new Set());

  // ── 左侧章节树：搜索 + 仅显示有关联 ────────────────────────────────
  readonly chapterKeyword = signal('');
  readonly onlyWithLinked = signal(false);
  readonly isChapterFiltering = computed(
    () => this.chapterKeyword().trim() !== '' || this.onlyWithLinked()
  );

  /** 各章节直接关联的习题总数（按 chapterIds 多对多累计） */
  readonly chapterExerciseCountMap = computed(() => {
    const map = new Map<string, number>();
    for (const ex of this.courseExercises()) {
      const ids = ex.chapterIds ?? (ex.chapterId ? [ex.chapterId] : []);
      for (const id of ids) {
        if (!id) continue;
        map.set(id, (map.get(id) || 0) + 1);
      }
    }
    return map;
  });

  /** 至少关联了一道习题的章节数（用于筛选文案） */
  readonly contentChapterCount = computed(() => {
    let count = 0;
    for (const v of this.chapterExerciseCountMap().values()) {
      if (v > 0) count++;
    }
    return count;
  });

  /** 过滤后的章节树：保留命中节点及其祖先链；筛选时模板自动全展开 */
  readonly visibleChapters = computed(() => {
    const kw = this.chapterKeyword().trim().toLowerCase();
    const onlyLinked = this.onlyWithLinked();
    if (!kw && !onlyLinked) return this.chapters();

    const filter = (nodes: ChapterDto[]): ChapterDto[] => {
      const out: ChapterDto[] = [];
      for (const n of nodes || []) {
        const children = filter(n.children || []);
        if (this.chapterSelfVisible(n, kw, onlyLinked) || children.length > 0) {
          out.push({ ...n, children });
        }
      }
      return out;
    };
    return filter(this.chapters());
  });

  // ── 右侧：当前选中章节 ───────────────────────────────────────────
  readonly selectedChapterId = signal<string | null>(null);
  readonly selectedChapterTitle = signal('');

  // 当前章节已关联的习题（服务端来源：by-chapter，已包含多对多）
  readonly chapterExercises = signal<ExerciseDto[]>([]);
  readonly courseExercises = signal<ExerciseDto[]>([]);
  readonly linkedLoading = signal(false);

  // 右侧表格分页
  readonly linkedPage = signal(1);
  readonly linkedPageSize = signal(10);
  readonly pagedLinkedExercises = computed(() => {
    const start = (this.linkedPage() - 1) * this.linkedPageSize();
    return this.chapterExercises().slice(start, start + this.linkedPageSize());
  });

  // ── 关联习题弹窗 ─────────────────────────────────────────────────
  readonly linkModalVisible = signal(false);
  readonly linkModalKeyword = signal('');
  readonly linkModalPage = signal(1);
  readonly linkModalPageSize = signal(8);
  readonly linkModalSelectedIds = signal<Set<string>>(new Set());
  readonly linkModalSubmitting = signal(false);
  /** 弹窗内是否展示已关联到当前章节的习题（默认隐藏，避免干扰“待添加”列表） */
  readonly showLinkedInModal = signal(false);

  /** 已关联到当前章节的习题 id 集合（在弹窗中以灰显 + “已关联”徽标呈现） */
  readonly alreadyLinkedToCurrentChapter = computed(() => {
    const chapterId = this.selectedChapterId();
    if (!chapterId) return new Set<string>();
    const set = new Set<string>();
    for (const e of this.courseExercises()) {
      const ids = e.chapterIds ?? [];
      if (e.id && ids.includes(chapterId)) set.add(e.id);
    }
    return set;
  });

  /** 弹窗内习题来源筛选：全部 / 手动 / AI生成 */
  readonly linkModalSource = signal<'all' | 'manual' | 'ai'>('all');

  /** 弹窗内的可选习题列表（搜索 + 来源过滤；默认不展示已关联的，开启开关后可一并查看） */
  readonly linkCandidates = computed(() => {
    const kw = this.linkModalKeyword().trim().toLowerCase();
    const showLinked = this.showLinkedInModal();
    const source = this.linkModalSource();
    let list = this.courseExercises();
    if (!showLinked) {
      const linked = this.alreadyLinkedToCurrentChapter();
      list = list.filter(e => !e.id || !linked.has(e.id));
    }
    if (source === 'ai') list = list.filter(e => e.isAiGenerated);
    else if (source === 'manual') list = list.filter(e => !e.isAiGenerated);
    if (kw) {
      list = list.filter(
        e =>
          (e.title || '').toLowerCase().includes(kw) ||
          (e.questionContent || '').toLowerCase().includes(kw)
      );
    }
    return list;
  });

  readonly pagedLinkCandidates = computed(() => {
    const start = (this.linkModalPage() - 1) * this.linkModalPageSize();
    return this.linkCandidates().slice(start, start + this.linkModalPageSize());
  });

  /** 当前分页内“可被选中”的习题 id（即未关联的） */
  readonly selectableOnPageIds = computed(() => {
    const linked = this.alreadyLinkedToCurrentChapter();
    return this.pagedLinkCandidates()
      .map(e => e.id)
      .filter((id): id is string => !!id && !linked.has(id));
  });

  readonly linkModalAllChecked = computed(() => {
    const ids = this.selectableOnPageIds();
    if (ids.length === 0) return false;
    const sel = this.linkModalSelectedIds();
    return ids.every(id => sel.has(id));
  });

  readonly linkModalIndeterminate = computed(() => {
    const ids = this.selectableOnPageIds();
    if (ids.length === 0) return false;
    const sel = this.linkModalSelectedIds();
    const some = ids.some(id => sel.has(id));
    return some && !this.linkModalAllChecked();
  });

  /** 弹窗底部真正要新增关联的题目数（已关联的不计入） */
  readonly linkModalEffectiveCount = computed(() => {
    const linked = this.alreadyLinkedToCurrentChapter();
    let n = 0;
    for (const id of this.linkModalSelectedIds()) {
      if (!linked.has(id)) n++;
    }
    return n;
  });

  ngOnInit() {
    this.pendingCourseId = this.route.snapshot.queryParamMap.get('courseId');
    this.pendingChapterId = this.route.snapshot.queryParamMap.get('chapterId');
    this.loadCourses();
  }

  loadCourses() {
    this.courseService.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe({
      next: result => {
        const items = result.items || [];
        this.courses.set(items);
        // 带 courseId 跳转进来：课程加载完成后自动选中并加载章节
        if (this.pendingCourseId && items.some(c => c.id === this.pendingCourseId)) {
          const courseId = this.pendingCourseId;
          this.pendingCourseId = null;
          this.onCourseSelected(courseId);
        }
      },
    });
  }

  onCourseSelected(courseId: string) {
    this.selectedCourseId.set(courseId);
    this.selectedChapterId.set(null);
    this.selectedChapterTitle.set('');
    this.chapterExercises.set([]);
    this.linkedPage.set(1);
    this.loadChapterTree();
    this.loadCourseExercises();
  }

  loadChapterTree() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.chapterService.getChapterTree(courseId).subscribe({
      next: data => {
        const list = data || [];
        this.chapters.set(list);
        // 默认只展开顶级章节
        const expanded = new Set<string>(list.map(n => n.id!).filter(Boolean));

        // 带 chapterId 跳转进来：章节树加载完成后自动选中该章节并展开其祖先链
        const pendingChapterId = this.pendingChapterId;
        if (pendingChapterId) {
          const node = this.findChapterNode(list, pendingChapterId);
          if (node) {
            this.expandAncestors(list, pendingChapterId, expanded);
            this.expandedNodes.set(new Set(expanded));
            this.pendingChapterId = null;
            this.selectChapter(node);
            return;
          }
        }

        this.expandedNodes.set(expanded);
      },
    });
  }

  /** 深度优先查找章节节点 */
  private findChapterNode(nodes: ChapterDto[], id: string): ChapterDto | null {
    for (const n of nodes) {
      if (n.id === id) return n;
      const found = this.findChapterNode(n.children || [], id);
      if (found) return found;
    }
    return null;
  }

  /** 将包含目标章节的所有祖先节点加入展开集合，返回目标是否在该子树内 */
  private expandAncestors(nodes: ChapterDto[], targetId: string, expanded: Set<string>): boolean {
    for (const n of nodes) {
      if (n.id === targetId) return true;
      if (n.children?.length && this.expandAncestors(n.children, targetId, expanded)) {
        if (n.id) expanded.add(n.id);
        return true;
      }
    }
    return false;
  }

  loadCourseExercises() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.exerciseService.getByCourse(courseId).subscribe({
      next: data => {
        this.courseExercises.set(data || []);
      },
    });
  }

  // ── 章节树交互 ──────────────────────────────────────────────────
  /** 章节项点击：选中该章节；若是父级且当前未展开则一并展开，避免下级目录“看不见” */
  onChapterItemClick(node: ChapterDto): void {
    if (!node.id) return;
    if (node.children?.length && !this.expandedNodes().has(node.id)) {
      const set = new Set(this.expandedNodes());
      set.add(node.id);
      this.expandedNodes.set(set);
    }
    this.selectChapter(node);
  }

  toggleChapter(event: MouseEvent, id: string): void {
    event.stopPropagation();
    const set = new Set(this.expandedNodes());
    if (set.has(id)) set.delete(id);
    else set.add(id);
    this.expandedNodes.set(set);
  }

  isExpanded(id: string): boolean {
    return this.expandedNodes().has(id);
  }

  hasChildren(node: ChapterDto): boolean {
    return !!node.children && node.children.length > 0;
  }

  /** 单个章节自身是否满足当前筛选条件（祖先链由 visibleChapters 保留） */
  private chapterSelfVisible(node: ChapterDto, kw: string, onlyLinked: boolean): boolean {
    if (onlyLinked) {
      const cnt = node.id ? this.chapterExerciseCountMap().get(node.id) || 0 : 0;
      if (cnt === 0) return false;
    }
    if (kw && !(node.title || '').toLowerCase().includes(kw)) return false;
    return true;
  }

  /** 清空章节搜索与筛选 */
  clearChapterFilter(): void {
    this.chapterKeyword.set('');
    this.onlyWithLinked.set(false);
  }

  selectChapter(chapter: ChapterDto) {
    this.selectedChapterId.set(chapter.id ?? null);
    this.selectedChapterTitle.set(chapter.title ?? '');
    this.linkedPage.set(1);
    this.loadChapterExercises();
  }

  loadChapterExercises() {
    const chapterId = this.selectedChapterId();
    if (!chapterId) return;

    this.linkedLoading.set(true);
    this.exerciseService.getByChapter(chapterId).subscribe({
      next: data => {
        this.chapterExercises.set(data || []);
        this.linkedLoading.set(false);
      },
      error: () => {
        this.linkedLoading.set(false);
        this.message.error('加载习题失败');
      },
    });
  }

  /** 右侧表格行：单题取消关联 */
  async unlinkExercise(exercise: ExerciseDto) {
    const currentChapterId = this.selectedChapterId();
    if (!exercise.id) return;

    const remainingIds = (exercise.chapterIds ?? []).filter(id => id !== currentChapterId);

    const dto: CreateUpdateExerciseDto = {
      courseId: exercise.courseId,
      chapterId: remainingIds.length > 0 ? remainingIds[0] : null,
      chapterIds: remainingIds,
      title: exercise.title,
      questionContent: exercise.questionContent,
      type: exercise.type,
      options: exercise.options,
      answer: exercise.answer,
      questionAnalysis: exercise.questionAnalysis,
      difficulty: exercise.difficulty,
      score: exercise.score,
    };

    try {
      await firstValueFrom(this.exerciseService.update(exercise.id, dto));
      this.message.success('已取消关联');
      this.loadChapterExercises();
      this.loadCourseExercises(); // 刷新 chapterIds，章节数徽标同步
    } catch {
      this.message.error('取消关联失败');
    }
  }

  onLinkedPageChange(page: number) {
    this.linkedPage.set(page);
  }

  // ── 关联习题弹窗 ───────────────────────────────────────────────
  openLinkModal() {
    if (!this.selectedChapterId()) {
      this.message.warning('请先选择章节');
      return;
    }
    this.linkModalKeyword.set('');
    this.linkModalPage.set(1);
    this.linkModalSelectedIds.set(new Set());
    this.showLinkedInModal.set(false);
    this.linkModalSource.set('all');
    this.linkModalVisible.set(true);
  }

  closeLinkModal() {
    this.linkModalVisible.set(false);
    this.linkModalSelectedIds.set(new Set());
    this.linkModalKeyword.set('');
    this.linkModalPage.set(1);
    this.showLinkedInModal.set(false);
  }

  /** 切换「显示已关联」开关：回到第一页，避免空选状态跨越分页 */
  onShowLinkedChange(value: boolean) {
    this.showLinkedInModal.set(value);
    this.linkModalPage.set(1);
  }

  onLinkKeywordChange(value: string) {
    this.linkModalKeyword.set(value);
    this.linkModalPage.set(1);
  }

  onLinkSourceChange(value: 'all' | 'manual' | 'ai') {
    this.linkModalSource.set(value);
    this.linkModalPage.set(1);
  }

  /** 跳转 AI 生成习题页（携带当前课程，便于生成后回来关联） */
  goAiGenerate() {
    const courseId = this.selectedCourseId();
    this.router.navigate(
      ['/ai/exercise-generate'],
      courseId ? { queryParams: { courseId } } : undefined
    );
  }

  onLinkPageChange(page: number) {
    this.linkModalPage.set(page);
  }

  isLinkSelected(id: string): boolean {
    return this.linkModalSelectedIds().has(id);
  }

  isAlreadyLinked(id: string | undefined): boolean {
    if (!id) return false;
    return this.alreadyLinkedToCurrentChapter().has(id);
  }

  toggleLinkOne(id: string | undefined, checked: boolean) {
    if (!id) return;
    const set = new Set(this.linkModalSelectedIds());
    if (checked) set.add(id);
    else set.delete(id);
    this.linkModalSelectedIds.set(set);
  }

  toggleLinkAll(checked: boolean) {
    const ids = this.selectableOnPageIds();
    if (ids.length === 0) return;
    const set = new Set(this.linkModalSelectedIds());
    for (const id of ids) {
      if (checked) set.add(id);
      else set.delete(id);
    }
    this.linkModalSelectedIds.set(set);
  }

  /** 批量关联：将弹窗内选中的“新”习题加入到当前章节的 chapterIds 中 */
  async confirmLinkExercises() {
    const chapterId = this.selectedChapterId();
    if (!chapterId) return;

    const selectedIds = Array.from(this.linkModalSelectedIds());
    const linked = this.alreadyLinkedToCurrentChapter();
    // 跳过已关联的，避免无意义的写
    const targetIds = selectedIds.filter(id => !linked.has(id));
    if (targetIds.length === 0) {
      this.message.warning('请先选择要关联的习题');
      return;
    }

    this.linkModalSubmitting.set(true);

    let successCount = 0;
    let failCount = 0;

    // 顺序提交避免并发覆盖 chapterIds（最后一个胜出会丢关联）
    for (const id of targetIds) {
      const exercise = this.courseExercises().find(e => e.id === id);
      if (!exercise || !exercise.id) continue;

      const existingIds = new Set(
        exercise.chapterIds?.filter((cid): cid is string => !!cid) ?? []
      );
      existingIds.add(chapterId);

      const dto: CreateUpdateExerciseDto = {
        courseId: exercise.courseId,
        chapterId: chapterId,
        chapterIds: Array.from(existingIds),
        title: exercise.title,
        questionContent: exercise.questionContent,
        type: exercise.type,
        options: exercise.options,
        answer: exercise.answer,
        questionAnalysis: exercise.questionAnalysis,
        difficulty: exercise.difficulty,
        score: exercise.score,
      };

      try {
        await firstValueFrom(this.exerciseService.update(exercise.id, dto));
        successCount++;
      } catch {
        failCount++;
      }
    }

    this.linkModalSubmitting.set(false);

    if (failCount === 0) {
      this.message.success(`已成功关联 ${successCount} 道习题`);
    } else {
      this.message.warning(`关联完成：成功 ${successCount}，失败 ${failCount}`);
    }

    this.closeLinkModal();
    this.loadChapterExercises();
    this.loadCourseExercises(); // 刷新章节数徽标
  }

  // ── 工具方法 ───────────────────────────────────────────────────
  getTypeName(type: ExerciseType | undefined): string {
    if (type === undefined) return '未知';
    const names: Record<number, string> = {
      [ExerciseType.SingleChoice]: '单选题',
      [ExerciseType.MultiChoice]: '多选题',
      [ExerciseType.TrueFalse]: '判断题',
      [ExerciseType.FillBlank]: '填空题',
      [ExerciseType.ShortAnswer]: '问答题',
      [ExerciseType.Essay]: '论述题',
      [ExerciseType.CaseAnalysis]: '案例分析',
    };
    return names[type] ?? '未知';
  }

  getTypeColor(type: ExerciseType | undefined): string {
    if (type === undefined) return 'default';
    const colors: Record<number, string> = {
      [ExerciseType.SingleChoice]: 'blue',
      [ExerciseType.MultiChoice]: 'purple',
      [ExerciseType.TrueFalse]: 'cyan',
      [ExerciseType.FillBlank]: 'orange',
      [ExerciseType.ShortAnswer]: 'gold',
      [ExerciseType.Essay]: 'red',
      [ExerciseType.CaseAnalysis]: 'green',
    };
    return colors[type] ?? 'default';
  }

  trackChapter = (_: number, n: ChapterDto) => n.id;
  trackExercise = (_: number, e: ExerciseDto) => e.id;
}