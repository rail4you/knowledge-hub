import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { catchError, of, Subscription, take, timer } from 'rxjs';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzModalModule } from 'ng-zorro-antd/modal';

import { ConfigStateService } from '@abp/ng.core';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { ChapterDto } from '../../proxy/courses/dtos/models';
import { LearningAnalyticsService } from '../../learning-analytics/services/learning-analytics.service';
import { KnowledgePointGraphComponent } from './knowledge-point-graph.component';
import {
  CourseExerciseItem,
  KnowledgePointDetail,
  KnowledgePointService,
  KnowledgeVectorStatus,
  KpExerciseLink,
  KpExerciseMapping,
  KpMergeSuggestion,
} from '../../learning-analytics/services/knowledge-point.service';

/** 弹窗中的一行：课程知识点 + 是否关联 + 权重 */
interface ModalKpRow {
  kp: KnowledgePointDetail;
  linked: boolean;
  weight: number;
  source: string;
}

/**
 * 智能知识点分析：归一化闭环
 * - Tab「知识点列表」：知识点显示（来源 / 操作）+ 灰区确认 / 合并 / 拆分 / 习题关联
 * - Tab「习题对应知识点」：一题多知识点，弹窗表格增删关联、批量设置权重
 * - AI 全量生成：章节 → 知识点、习题 → 知识点关联一次跑完，并重建语义索引
 */
@Component({
  selector: 'app-knowledge-point-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzEmptyModule,
    NzModalModule,
    KnowledgePointGraphComponent,
  ],
  templateUrl: './knowledge-point-management.component.html',
  styleUrls: ['./knowledge-point-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KnowledgePointManagementComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
  private readonly configState = inject(ConfigStateService);
  private readonly chapterService = inject(ChapterService);
  private readonly exerciseService = inject(ExerciseService);
  private readonly kpService = inject(KnowledgePointService);
  private readonly analytics = inject(LearningAnalyticsService);

  private sub = new Subscription();
  /** 生成任务跑批期间的定时刷新 */
  private autoRefresh?: Subscription;

  // 课程 / 章节 / 习题
  courses = signal<CourseDto[]>([]);
  chapters = signal<ChapterDto[]>([]);
  exercises = signal<CourseExerciseItem[]>([]);
  selectedCourseId = '';
  /** 只看我教的课程（按 Course.TeacherId 过滤，空则回退全部） */
  onlyMyCourses = false;
  readonly myCoursesEmpty = signal(false);

  // 列表
  /** 视图模式：列表 / 图谱 */
  readonly viewMode = signal<'list' | 'graph'>('list');
  /** 改变后让图谱组件重新拉取（合并/编辑/拆分后刷新） */
  readonly graphRefresh = signal(0);

  /** 页签：知识点列表 / 习题对应知识点 */
  readonly activeTab = signal<'points' | 'exercises'>('points');

  readonly loading = signal(false);
  readonly items = signal<KnowledgePointDetail[]>([]);
  readonly totalCount = signal(0);
  pageIndex = 1;
  pageSize = 20;
  filterText = '';
  statusFilter: number | null = null;
  sourceFilter = '';

  // 合并建议
  readonly suggestions = signal<KpMergeSuggestion[]>([]);
  readonly suggestionLoading = signal(false);
  readonly hiddenSuggestions = signal<string[]>([]);
  /** 合并建议范围：course 同课程 / tenant 跨课程（走 pgvector ANN） */
  suggestionScope: 'course' | 'tenant' = 'course';
  readonly vectorStatus = signal<KnowledgeVectorStatus | null>(null);

  // 编辑
  readonly editing = signal<KnowledgePointDetail | null>(null);
  editName = '';
  editDescription = '';
  editChapterId: string | null = null;
  editSyllabusOrder = 0;
  readonly savingEdit = signal(false);

  // 知识点级判定线
  kpWeakPercent = 60;
  kpOkPercent = 75;
  readonly thresholdSaving = signal(false);
  readonly courseThresholdHint = signal('');

  // 习题关联
  readonly linkPanelKp = signal<KnowledgePointDetail | null>(null);
  readonly links = signal<KpExerciseLink[]>([]);
  readonly linksLoading = signal(false);
  newLinkExerciseId = '';
  newLinkWeight = 1;

  // ══════════════ 习题对应知识点（Tab） ══════════════
  /** 课程内习题 × 知识点映射 */
  readonly mappingRows = signal<KpExerciseMapping[]>([]);
  readonly mappingLoading = signal(false);
  /** 搜索：习题标题 / 题干 / 知识点名 */
  mappingKeyword = '';
  /** 章节筛选 */
  mappingChapterId = '';

  /** 配置弹窗：当前编辑的习题 */
  readonly mappingModal = signal<KpExerciseMapping | null>(null);
  /** 弹窗表格：课程全部知识点 + 勾选状态 + 权重 */
  readonly modalRows = signal<ModalKpRow[]>([]);
  readonly modalLoading = signal(false);
  readonly modalSaving = signal(false);
  /** 批量设置的权重值 */
  batchWeight = 1;

  // 拆分
  readonly splitPanelKp = signal<KnowledgePointDetail | null>(null);
  splitName = '';
  splitChapterId: string | null = null;
  splitExerciseIds: string[] = [];

  // 合并
  readonly mergePanelKp = signal<KnowledgePointDetail | null>(null);
  mergeTargetId = '';

  // 提示
  readonly notice = signal('');
  readonly busy = signal('');

  readonly pendingCount = computed(() => this.items().filter(i => i.status === 1).length);
  readonly visibleSuggestions = computed(() =>
    this.suggestions().filter(s => !this.hiddenSuggestions().includes(`${s.sourceId}-${s.targetId}`))
  );

  ngOnInit(): void {
    this.loadCourses();
  }

  ngOnDestroy(): void {
    this.stopAutoRefresh();
    this.sub.unsubscribe();
  }

  private currentUserId(): string | undefined {
    const user = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    return (user?.['id'] as string) || undefined;
  }

  onMyCoursesToggle(value: boolean): void {
    this.onlyMyCourses = value;
    this.selectedCourseId = '';
    this.loadCourses();
  }

  private loadCourses(): void {
    const teacherId = this.onlyMyCourses ? this.currentUserId() : undefined;
    this.sub.add(
      this.courseService
        .getList({ skipCount: 0, maxResultCount: 200, teacherId } as never)
        .pipe(catchError(() => this.courseService.getPublished({ skipCount: 0, maxResultCount: 200 } as never)))
        .subscribe({
          next: res => {
            const items = res?.items ?? [];
            if (this.onlyMyCourses && items.length === 0) {
              this.myCoursesEmpty.set(true);
              this.onlyMyCourses = false;
              this.loadCourses();
              return;
            }

            this.myCoursesEmpty.set(false);
            this.courses.set(items);
            if (items.length) {
              this.selectedCourseId = items[0].id;
              this.onCourseChange(this.selectedCourseId);
            }
          },
          error: () => this.courses.set([]),
        })
    );
  }

  onCourseChange(courseId: string): void {
    this.selectedCourseId = courseId;
    this.pageIndex = 1;
    this.notice.set('');
    this.closePanels();
    this.stopAutoRefresh();
    this.loadChapters(courseId);
    this.loadExercises(courseId);
    this.loadList();
    this.loadMappings();
    this.loadSuggestions();
    this.loadVectorStatus();
  }

  private loadChapters(courseId: string): void {
    this.sub.add(
      this.chapterService.getChaptersByCourse(courseId).subscribe({
        next: list => this.chapters.set(list ?? []),
        error: () => this.chapters.set([]),
      })
    );
  }

  private loadExercises(courseId: string): void {
    this.sub.add(
      this.exerciseService.getByCourse(courseId).subscribe({
        next: list => this.exercises.set((list ?? []) as CourseExerciseItem[]),
        error: () => this.exercises.set([]),
      })
    );
  }

  loadList(): void {
    if (!this.selectedCourseId) return;
    this.loading.set(true);
    this.sub.add(
      this.kpService
        .getList({
          courseId: this.selectedCourseId,
          filter: this.filterText,
          status: this.statusFilter,
          source: this.sourceFilter || null,
          skipCount: (this.pageIndex - 1) * this.pageSize,
          maxResultCount: this.pageSize,
        })
        .subscribe({
          next: res => {
            this.items.set(res?.items ?? []);
            this.totalCount.set(res?.totalCount ?? 0);
            this.loading.set(false);
          },
          error: () => {
            this.items.set([]);
            this.totalCount.set(0);
            this.loading.set(false);
          },
        })
    );
  }

  applyFilter(): void {
    this.pageIndex = 1;
    this.loadList();
  }

  resetFilter(): void {
    this.filterText = '';
    this.statusFilter = null;
    this.sourceFilter = '';
    this.pageIndex = 1;
    this.loadList();
  }

  pageCount(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }

  prevPage(): void {
    if (this.pageIndex > 1) {
      this.pageIndex--;
      this.loadList();
    }
  }

  nextPage(): void {
    if (this.pageIndex < this.pageCount()) {
      this.pageIndex++;
      this.loadList();
    }
  }

  // ══════════════ 灰区确认 / 编辑 ══════════════

  confirmKp(kp: KnowledgePointDetail): void {
    this.busy.set(kp.id);
    this.sub.add(
      this.kpService.confirm(kp.id).subscribe({
        next: updated => {
          this.busy.set('');
          this.notice.set(`「${updated.name}」已确认为正式知识点`);
          this.loadList();
          this.loadSuggestions();
          this.refreshGraph();
        },
        error: () => {
          this.busy.set('');
          this.notice.set('确认失败，请重试');
        },
      })
    );
  }

  startEdit(kp: KnowledgePointDetail): void {
    this.splitPanelKp.set(null);
    this.linkPanelKp.set(null);
    this.mergePanelKp.set(null);
    this.editing.set(kp);
    this.editName = kp.name;
    this.editDescription = kp.description ?? '';
    this.editChapterId = kp.chapterId ?? null;
    this.editSyllabusOrder = kp.syllabusOrder;
    this.loadKpThreshold(kp);
  }

  /** 加载该知识点的生效判定线（含课程级参考值） */
  private loadKpThreshold(kp: KnowledgePointDetail): void {
    this.thresholdNoticeByKp.set('');
    this.sub.add(
      this.analytics.getThreshold(kp.courseId, kp.id).subscribe({
        next: t => {
          this.kpWeakPercent = Math.round((t.weakLine ?? kp.weakLine) * 100);
          this.kpOkPercent = Math.round((t.okLine ?? kp.targetLine) * 100);
          this.courseThresholdHint.set(
            t.isOverride
              ? '当前使用知识点级判定线'
              : `当前使用课程级判定线${
                  t.courseWeakLine != null ? `（${Math.round(t.courseWeakLine * 100)}% / ${Math.round(t.courseOkLine! * 100)}%）` : '（系统默认 60% / 75%）'
                }`
          );
        },
        error: () => this.courseThresholdHint.set(''),
      })
    );
  }

  readonly thresholdNoticeByKp = signal('');

  /** 保存知识点级判定线（仅影响等级划分，不改掌握度） */
  saveKpThreshold(): void {
    const kp = this.editing();
    if (!kp) return;

    const weak = Number(this.kpWeakPercent) / 100;
    const ok = Number(this.kpOkPercent) / 100;
    if (!(weak >= 0 && ok <= 1 && weak < ok)) {
      this.thresholdNoticeByKp.set('判定线不合法：需满足 0 ≤ 薄弱线 < 达标线 ≤ 100%');
      return;
    }

    this.thresholdSaving.set(true);
    this.sub.add(
      this.analytics
        .setThreshold({ courseId: kp.courseId, knowledgePointId: kp.id, weakLine: weak, okLine: ok })
        .subscribe({
          next: () => {
            this.thresholdSaving.set(false);
            this.thresholdNoticeByKp.set(`已保存判定线：${this.kpWeakPercent}% / ${this.kpOkPercent}%`);
            this.loadList();
            this.refreshGraph();
          },
          error: err => {
            this.thresholdSaving.set(false);
            this.thresholdNoticeByKp.set(err?.error?.error?.message ?? '判定线保存失败');
          },
        })
    );
  }

  /** 清除知识点级覆盖，恢复课程级 */
  clearKpThreshold(): void {
    const kp = this.editing();
    if (!kp) return;
    this.thresholdSaving.set(true);
    this.sub.add(
      this.analytics.clearThreshold({ courseId: kp.courseId, knowledgePointId: kp.id }).subscribe({
        next: () => {
          this.thresholdSaving.set(false);
          this.thresholdNoticeByKp.set('已恢复课程级判定线');
          this.loadList();
          this.refreshGraph();
        },
        error: () => {
          this.thresholdSaving.set(false);
          this.thresholdNoticeByKp.set('清除覆盖失败');
        },
      })
    );
  }

  cancelEdit(): void {
    this.editing.set(null);
  }

  saveEdit(): void {
    const kp = this.editing();
    if (!kp) return;
    if (!this.editName.trim()) {
      this.notice.set('知识点名称不能为空');
      return;
    }

    this.savingEdit.set(true);
    this.sub.add(
      this.kpService
        .update(kp.id, {
          name: this.editName.trim(),
          chapterId: this.editChapterId,
          description: this.editDescription || null,
          syllabusOrder: Number(this.editSyllabusOrder) || 0,
        })
        .subscribe({
          next: updated => {
            this.savingEdit.set(false);
            this.editing.set(null);
            this.notice.set(`已保存「${updated.name}」`);
            this.loadList();
            this.refreshGraph();
          },
          error: err => {
            this.savingEdit.set(false);
            this.notice.set(err?.error?.error?.message ?? '保存失败');
          },
        })
    );
  }

  removeKp(kp: KnowledgePointDetail): void {
    if (!confirm(`确定删除知识点「${kp.name}」？其习题关联与掌握度记录会一并删除，且不可恢复。`)) {
      return;
    }

    this.busy.set(kp.id);
    this.sub.add(
      this.kpService.remove(kp.id).subscribe({
        next: () => {
          this.busy.set('');
          this.notice.set(`已删除「${kp.name}」`);
          this.loadList();
          this.loadSuggestions();
          this.refreshGraph();
        },
        error: () => {
          this.busy.set('');
          this.notice.set('删除失败，请重试');
        },
      })
    );
  }

  // ══════════════ 合并 ══════════════

  startMerge(kp: KnowledgePointDetail): void {
    this.editing.set(null);
    this.linkPanelKp.set(null);
    this.splitPanelKp.set(null);
    this.mergePanelKp.set(kp);
    this.mergeTargetId = '';
  }

  acceptSuggestion(s: KpMergeSuggestion): void {
    this.doMerge(s.sourceId, s.targetId);
  }

  reverseSuggestion(s: KpMergeSuggestion): void {
    this.doMerge(s.targetId, s.sourceId);
  }

  ignoreSuggestion(s: KpMergeSuggestion): void {
    this.hiddenSuggestions.update(list => [...list, `${s.sourceId}-${s.targetId}`]);
  }

  confirmMerge(): void {
    const kp = this.mergePanelKp();
    if (!kp || !this.mergeTargetId) {
      this.notice.set('请选择要合并到的目标知识点');
      return;
    }

    this.doMerge(kp.id, this.mergeTargetId);
  }

  private doMerge(sourceId: string, targetId: string): void {
    this.busy.set(sourceId);
    this.sub.add(
      this.kpService.merge(sourceId, targetId).subscribe({
        next: res => {
          this.busy.set('');
          this.mergePanelKp.set(null);
          this.notice.set(res?.message ?? '合并完成');
          this.loadList();
          this.loadSuggestions();
          this.refreshGraph();
        },
        error: err => {
          this.busy.set('');
          this.notice.set(err?.error?.error?.message ?? '合并失败');
        },
      })
    );
  }

  mergeCandidates(kp: KnowledgePointDetail | null): KnowledgePointDetail[] {
    if (!kp) return [];
    return this.items().filter(i => i.id !== kp.id);
  }

  // ══════════════ 拆分 ══════════════

  startSplit(kp: KnowledgePointDetail): void {
    this.editing.set(null);
    this.linkPanelKp.set(null);
    this.mergePanelKp.set(null);
    this.splitPanelKp.set(kp);
    this.splitName = `${kp.name}（细分）`;
    this.splitChapterId = kp.chapterId ?? null;
    this.splitExerciseIds = [];

    this.linksLoading.set(true);
    this.sub.add(
      this.kpService.getExerciseLinks(kp.id).subscribe({
        next: list => {
          this.links.set(list ?? []);
          this.linksLoading.set(false);
        },
        error: () => {
          this.links.set([]);
          this.linksLoading.set(false);
        },
      })
    );
  }

  toggleSplitExercise(exerciseId: string, checked: boolean): void {
    this.splitExerciseIds = checked
      ? [...this.splitExerciseIds, exerciseId]
      : this.splitExerciseIds.filter(id => id !== exerciseId);
  }

  isSplitSelected(exerciseId: string): boolean {
    return this.splitExerciseIds.includes(exerciseId);
  }

  confirmSplit(): void {
    const kp = this.splitPanelKp();
    if (!kp) return;
    if (!this.splitName.trim()) {
      this.notice.set('请填写新知识点名称');
      return;
    }
    if (!this.splitExerciseIds.length) {
      this.notice.set('请至少选择一道要拆出的习题');
      return;
    }

    this.busy.set(kp.id);
    this.sub.add(
      this.kpService
        .split({
          knowledgePointId: kp.id,
          newName: this.splitName.trim(),
          chapterId: this.splitChapterId,
          exerciseIds: this.splitExerciseIds,
        })
        .subscribe({
          next: created => {
            this.busy.set('');
            this.splitPanelKp.set(null);
            this.notice.set(`已拆分为新知识点「${created.name}」（${this.splitExerciseIds.length} 道习题）`);
            this.loadList();
            this.refreshGraph();
          },
          error: err => {
            this.busy.set('');
            this.notice.set(err?.error?.error?.message ?? '拆分失败');
          },
        })
    );
  }

  // ══════════════ 习题关联 ══════════════

  startLinks(kp: KnowledgePointDetail): void {
    this.editing.set(null);
    this.splitPanelKp.set(null);
    this.mergePanelKp.set(null);
    this.linkPanelKp.set(kp);
    this.newLinkExerciseId = '';
    this.newLinkWeight = 1;
    this.reloadLinks(kp.id);
  }

  private reloadLinks(kpId: string): void {
    this.linksLoading.set(true);
    this.sub.add(
      this.kpService.getExerciseLinks(kpId).subscribe({
        next: list => {
          this.links.set(list ?? []);
          this.linksLoading.set(false);
        },
        error: () => {
          this.links.set([]);
          this.linksLoading.set(false);
        },
      })
    );
  }

  addLink(): void {
    const kp = this.linkPanelKp();
    if (!kp || !this.newLinkExerciseId) {
      this.notice.set('请选择要关联的习题');
      return;
    }

    this.sub.add(
      this.kpService
        .addExerciseLink({
          knowledgePointId: kp.id,
          exerciseId: this.newLinkExerciseId,
          weight: Number(this.newLinkWeight) || 1,
        })
        .subscribe({
          next: () => {
            this.newLinkExerciseId = '';
            this.newLinkWeight = 1;
            this.notice.set('已关联习题（同题多知识点权重已自动归一化）');
            this.reloadLinks(kp.id);
            this.loadList();
            this.refreshGraph();
          },
          error: err => this.notice.set(err?.error?.error?.message ?? '关联失败'),
        })
    );
  }

  saveLinkWeight(link: KpExerciseLink): void {
    const kp = this.linkPanelKp();
    if (!kp) return;
    this.sub.add(
      this.kpService.updateExerciseLink({ linkId: link.id, weight: Number(link.weight) || 0.01 }).subscribe({
        next: () => {
          this.reloadLinks(kp.id);
          this.notice.set('权重已保存');
        },
        error: () => this.notice.set('权重保存失败'),
      })
    );
  }

  removeLink(link: KpExerciseLink): void {
    const kp = this.linkPanelKp();
    if (!kp) return;
    this.sub.add(
      this.kpService.removeExerciseLink(link.id).subscribe({
        next: () => {
          this.reloadLinks(kp.id);
          this.loadList();
        },
        error: () => this.notice.set('移除失败'),
      })
    );
  }

  /** 尚未关联的习题（用于下拉） */
  availableExercises(): CourseExerciseItem[] {
    const linked = new Set(this.links().map(l => l.exerciseId));
    return this.exercises().filter(e => !linked.has(e.id));
  }

  // ══════════════ AI / 向量 ══════════════

  /**
   * AI 全量生成知识点（合并了「AI 抽取 / 重新抽取 / 重建语义索引」）：
   * 章节 → 知识点、习题 → 知识点关联全量跑一遍，并按需重建语义索引。
   * 任务在后台执行，这里只负责提交并定时刷新。
   */
  generateKnowledgePoints(): void {
    if (!this.selectedCourseId) return;
    this.busy.set('generate');
    this.sub.add(
      this.kpService.generate(this.selectedCourseId).subscribe({
        next: res => {
          this.busy.set('');
          this.notice.set(res.message || '已提交知识点生成任务，完成后自动刷新');
          this.startAutoRefresh();
        },
        error: err => {
          this.busy.set('');
          this.notice.set(err?.error?.error?.message ?? '知识点生成提交失败');
        },
      })
    );
  }

  /** 生成任务在后台跑：每 12 秒刷新一次，最多约 2 分钟 */
  private startAutoRefresh(): void {
    this.stopAutoRefresh();
    const subscription = timer(12000, 12000)
      .pipe(take(10))
      .subscribe(() => {
        this.loadList();
        this.loadMappings();
        this.loadVectorStatus();
      });
    this.autoRefresh = subscription;
    this.sub.add(subscription);
  }

  private stopAutoRefresh(): void {
    this.autoRefresh?.unsubscribe();
    this.autoRefresh = undefined;
  }

  // ══════════════ 习题对应知识点（Tab） ══════════════

  loadMappings(): void {
    if (!this.selectedCourseId) return;
    this.mappingLoading.set(true);
    this.sub.add(
      this.kpService.getExerciseMappings(this.selectedCourseId).subscribe({
        next: list => {
          this.mappingRows.set(list ?? []);
          this.mappingLoading.set(false);
        },
        error: () => {
          this.mappingRows.set([]);
          this.mappingLoading.set(false);
        },
      })
    );
  }

  /** 过滤后的习题映射（按题目 / 题干 / 知识点名 + 章节） */
  filteredMappings(): KpExerciseMapping[] {
    const keyword = this.mappingKeyword.trim().toLowerCase();
    const chapterId = this.mappingChapterId;

    return this.mappingRows().filter(row => {
      if (chapterId && row.chapterId !== chapterId) return false;
      if (!keyword) return true;
      if ((row.exerciseTitle ?? '').toLowerCase().includes(keyword)) return true;
      if ((row.questionContent ?? '').toLowerCase().includes(keyword)) return true;
      return (row.links ?? []).some(l => (l.knowledgePointName ?? '').toLowerCase().includes(keyword));
    });
  }

  /** 打开「配置习题知识点」弹窗：列出课程全部知识点供勾选 */
  openMappingModal(row: KpExerciseMapping): void {
    this.mappingModal.set(row);
    this.modalLoading.set(true);
    this.batchWeight = 1;

    this.sub.add(
      this.kpService.getCourseKnowledgePoints(this.selectedCourseId).subscribe({
        next: kps => {
          const linkedByKp = new Map((row.links ?? []).map(l => [l.knowledgePointId ?? '', l]));
          this.modalRows.set(
            (kps ?? []).map(kp => {
              const link = linkedByKp.get(kp.id);
              return {
                kp,
                linked: !!link,
                weight: link?.weight ?? 1,
                source: link?.source ?? '',
              };
            })
          );
          this.modalLoading.set(false);
        },
        error: () => {
          this.modalRows.set([]);
          this.modalLoading.set(false);
        },
      })
    );
  }

  closeMappingModal(): void {
    this.mappingModal.set(null);
    this.modalRows.set([]);
  }

  /** 勾选 / 取消勾选某个知识点（勾选时给一个均分后的初始权重） */
  toggleModalKp(kpId: string, checked: boolean): void {
    this.modalRows.update(rows => {
      const next = rows.map(r => (r.kp.id === kpId ? { ...r, linked: checked } : r));
      if (!checked) return next;

      const linkedCount = next.filter(r => r.linked).length;
      if (linkedCount <= 0) return next;

      const even = Math.round((1 / linkedCount) * 100) / 100;
      return next.map(r => (r.kp.id === kpId ? { ...r, weight: even } : r));
    });
  }

  /** 批量设置权重：应用到所有已勾选的知识点 */
  applyBatchWeight(): void {
    const value = Number(this.batchWeight);
    if (!value || value <= 0) {
      this.notice.set('请输入大于 0 的权重');
      return;
    }

    this.modalRows.update(rows => rows.map(r => (r.linked ? { ...r, weight: value } : r)));
  }

  /** 均分：所有已勾选的知识点取相同权重 */
  applyEvenWeight(): void {
    const linked = this.modalRows().filter(r => r.linked);
    if (!linked.length) {
      this.notice.set('请先勾选要关联的知识点');
      return;
    }

    const even = Math.round((1 / linked.length) * 100) / 100;
    this.modalRows.update(rows => rows.map(r => (r.linked ? { ...r, weight: even } : r)));
  }

  /** 保存弹窗里的关联（增删 + 批量权重） */
  saveMapping(): void {
    const row = this.mappingModal();
    if (!row) return;

    const links = this.modalRows()
      .filter(r => r.linked)
      .map(r => ({ knowledgePointId: r.kp.id, weight: Number(r.weight) || 0 }));

    if (!links.length && !confirm(`确定移除「${row.exerciseTitle}」的全部知识点关联？`)) {
      return;
    }

    this.modalSaving.set(true);
    this.sub.add(
      this.kpService.saveExerciseMappings({ exerciseId: row.exerciseId, links }).subscribe({
        next: updated => {
          this.modalSaving.set(false);
          this.closeMappingModal();
          this.notice.set(
            links.length
              ? `已保存「${updated.exerciseTitle}」的知识点关联（${updated.links.length} 个）`
              : `已移除「${updated.exerciseTitle}」的全部知识点关联`
          );
          this.loadMappings();
          this.loadList();
          this.refreshGraph();
        },
        error: err => {
          this.modalSaving.set(false);
          this.notice.set(err?.error?.error?.message ?? '保存失败');
        },
      })
    );
  }

  loadVectorStatus(): void {
    if (!this.selectedCourseId) return;
    this.sub.add(
      this.kpService.getVectorStatus(this.selectedCourseId).subscribe({
        next: status => this.vectorStatus.set(status),
        error: () => this.vectorStatus.set(null),
      })
    );
  }

  onSuggestionScopeChange(scope: 'course' | 'tenant'): void {
    this.suggestionScope = scope;
    this.loadSuggestions();
  }

  loadSuggestions(): void {
    if (!this.selectedCourseId) return;
    this.suggestionLoading.set(true);
    this.sub.add(
      this.kpService.getMergeSuggestions(this.selectedCourseId, 0.75, this.suggestionScope).subscribe({
        next: list => {
          this.suggestions.set(list ?? []);
          this.suggestionLoading.set(false);
        },
        error: () => {
          this.suggestions.set([]);
          this.suggestionLoading.set(false);
        },
      })
    );
  }

  // ══════════════ 展示辅助 ══════════════

  /** 某题关联知识点的权重合计（正常为 1） */
  weightSum(row: KpExerciseMapping): number {
    return (row.links ?? []).reduce((sum, l) => sum + (Number(l.weight) || 0), 0);
  }

  /** 弹窗里已勾选的知识点数 */
  modalLinkedCount(): number {
    return this.modalRows().filter(r => r.linked).length;
  }

  /** 弹窗表格里单个知识点的权重 */
  setModalWeight(kpId: string, weight: number): void {
    this.modalRows.update(rows =>
      rows.map(r => (r.kp.id === kpId ? { ...r, weight: Number(weight) || 0 } : r))
    );
  }

  setViewMode(mode: 'list' | 'graph'): void {
    this.viewMode.set(mode);
    if (mode === 'graph') {
      this.closePanels();
      this.graphRefresh.update(v => v + 1);
    }
  }

  /** 图谱节点点击 → 打开该知识点的编辑面板（含判定线、关联、合并等入口） */
  onGraphNodeSelected(kp: KnowledgePointDetail): void {
    this.startEdit(kp);
  }

  private refreshGraph(): void {
    this.graphRefresh.update(v => v + 1);
  }

  private closePanels(): void {
    this.editing.set(null);
    this.linkPanelKp.set(null);
    this.splitPanelKp.set(null);
    this.mergePanelKp.set(null);
  }

  sourceLabel(source: string): string {
    return source === 'Llm' ? 'AI 抽取' : source === 'Manual' ? '手工' : '章节初始化';
  }

  statusLabel(status: number): string {
    return status === 1 ? '待确认' : '已确认';
  }

  similarityPercent(v: number): number {
    return Math.round((v ?? 0) * 100);
  }
}
