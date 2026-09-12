import { Component, signal, inject, computed, ChangeDetectionStrategy, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzStepsModule } from 'ng-zorro-antd/steps';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subject, Subscription, takeUntil } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import {
  ChatService,
  ResourceForChat,
  LessonPlanChapter,
  LessonPlanStreamEvent
} from '../services/chat.service';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { AiGenerationTaskDto, AiTaskService, AiTaskStatus, AiTaskType } from '../services/ai-task.service';
import { AiTaskNotificationService } from '../services/ai-task-notification.service';

interface LessonPlanInput {
  topic: string;
  subject: string;
  grade: string;
  duration: number;
  customPrompt: string;
}

/** 一份课堂教案（多章节方案中每个章节对应的独立教案）。 */
interface LessonPlanResult {
  title: string;
  subject: string;
  grade: string;
  duration: number;
  objectives: string[];
  keyPoints: string[];
  difficulties: string[];
  sections: { name: string; duration: number; content: string; activities: string[] }[];
  methods: string[];
  resources: string[];
  assessment: string[];
  homework: string[];
}

/** 整体教案（课程总览 + 多份章节教案）。 */
interface MultiChapterPlan {
  courseTitle: string;
  subject: string;
  grade: string;
  duration: number;
  courseObjectives: string[];
  chapters: { order: number; chapterTitle: string; lessonPlan: LessonPlanResult }[];
}

/** 章节来源：自动解析（AI） 或 手动填写。 */
type ChapterSource = 'auto' | 'manual';

type Phase = 'config' | 'chapters' | 'generating';

/** 历史记录项：仅保留多章节方案（与后端 AiTaskType.LessonPlanMulti 对应）。 */
interface LessonPlanHistoryItem {
  id: string;
  title: string;
  subject: string;
  grade: string;
  duration: number;
  chapterCount?: number;
  resourceName: string;
  resourceId: string;
  createdAt: string;
  multiResult: MultiChapterPlan;
  rawJson: string;
}

@Component({
  selector: 'app-lesson-plan',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzFormModule,
    NzSelectModule,
    NzInputNumberModule,
    NzDividerModule,
    NzCollapseModule,
    NzDescriptionsModule,
    NzTagModule,
    NzSpinModule,
    NzIconModule,
    NzEmptyModule,
    NzTableModule,
    NzPopconfirmModule,
    NzTooltipModule,
    NzProgressModule,
    NzStepsModule,
    NzTabsModule,
    NzRadioModule,
    FilePreviewComponent
  ],
  templateUrl: './lesson-plan.component.html',
  styleUrls: ['./lesson-plan.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LessonPlanComponent implements OnInit, OnDestroy {
  private readonly chatService = inject(ChatService);
  private readonly messageService = inject(NzMessageService);
  private readonly configState = inject(ConfigStateService);
  private readonly aiTaskService = inject(AiTaskService);
  private readonly aiTaskNotifications = inject(AiTaskNotificationService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy$ = new Subject<void>();
  @ViewChild('filePreview') filePreview!: FilePreviewComponent;
  private readonly HISTORY_KEY_PREFIX = 'kh-lesson-plan-history';
  private historyKey = `${this.HISTORY_KEY_PREFIX}:anon`;
  private readonly MAX_HISTORY = 20;

  /**
   * 根据当前登录租户生成 localStorage key，避免不同租户在同一浏览器里互相看到历史记录。
   * - 宿主 / 未登录 / 无 tenantId 时落到 :host
   * - 普通租户落到 :tenant:<tenantId>
   */
  private computeHistoryKey(): string {
    const tenantId = this.configState.getDeep('currentUser.tenantId') as string | null | undefined;
    return tenantId
      ? `${this.HISTORY_KEY_PREFIX}:tenant:${tenantId}`
      : `${this.HISTORY_KEY_PREFIX}:host`;
  }

  // ---------- resources ----------
  resources = signal<ResourceForChat[]>([]);
  resourcesLoading = signal(false);
  selectedResourceId = signal<string | null>(null);
  resourceFilter = signal('');

  selectedResource = computed(() => {
    const id = this.selectedResourceId();
    if (!id) return null;
    return this.resources().find(r => r.id === id) ?? null;
  });

  availableResources = computed(() => {
    // 教案生成口径一致：有全文索引或有摘要的资源都能生成
    return this.resources().filter(r => r.hasSummary === true || r.hasPageIndex === true);
  });

  filteredResources = computed(() => {
    const kw = this.resourceFilter().trim().toLowerCase();
    const list = this.availableResources();
    if (!kw) return list;
    return list.filter(r => r.name.toLowerCase().includes(kw) || (r.sourceFormat ?? '').toLowerCase().includes(kw));
  });

  resourceReady = computed(() => {
    const r = this.selectedResource();
    if (!r) return false;
    return r.hasSummary === true || r.hasPageIndex === true;
  });

  // ---------- form input ----------
  input = signal<LessonPlanInput>({
    topic: '',
    subject: '',
    grade: '',
    duration: 45,
    customPrompt: ''
  });

  // ---------- workflow state ----------
  /** 章节来源：自动解析（AI）或手动填写。决定「下一步」按钮行为。 */
  chapterSource = signal<ChapterSource>('auto');
  phase = signal<Phase>('config');
  /** 0 = 生成教案，1 = 历史记录（生成 UI 与结果 UI 用 Tab 分开） */
  readonly genTab = signal(0);

  // 固定 4 步：配置 → 解析/填写章节 → 生成教案 → 完成
  readonly steps = ['配置', '章节', '生成教案', '完成'];

  stepIndex = computed(() => {
    const phase = this.phase();
    return phase === 'config' ? 0 : phase === 'chapters' ? 1 : phase === 'generating' ? 2 : 3;
  });

  // ---------- chapter management ----------
  // 章节编辑项：服务端字段 + 本地稳定 key（跨上移/下移/删除保持展开态与高亮定位准确）
  chapters = signal<(LessonPlanChapter & { key: string })[]>([]);
  courseTitleHint = signal('');
  parsing = signal(false);
  parseRaw = signal('');
  // 章节搜索关键词
  chapterFilter = signal('');
  // 展开的章节 key 集合（嵌套表格的展开行：内容要点编辑区）
  expandedKeys = signal<string[]>([]);
  // 新增章节高亮行的 key
  flashKey = signal<string | null>(null);
  /** 搜索过滤后的章节（携带原数组下标，供增删改操作定位） */
  filteredChapters = computed(() => {
    const kw = this.chapterFilter().trim().toLowerCase();
    const all = this.chapters().map((c, index) => ({ ...c, index }));
    if (!kw) return all;
    return all.filter(c =>
      (c.title || '').toLowerCase().includes(kw) ||
      (c.summary || '').toLowerCase().includes(kw)
    );
  });
  /** 当前可见行是否全部展开 */
  allExpanded = computed(() => {
    const rows = this.filteredChapters();
    if (rows.length === 0) return false;
    const set = new Set(this.expandedKeys());
    return rows.every(r => set.has(r.key));
  });

  // ---------- generation ----------
  multiResult = signal<MultiChapterPlan | null>(null);
  rawJson = signal('');
  isLoading = signal(false);
  isExporting = signal(false);
  genError = signal('');

  progress = signal(0);
  progressMessage = signal('');
  activeChapter = signal(0);
  chapterTotal = signal(0);
  // 多章节生成：已用时（秒）+ 取消订阅句柄。单章大模型调用常需 30~120 秒，
  // 用已用时 + 后端 5 秒心跳让用户感知进度，而不是静止不动看似"卡死"。
  genElapsed = signal(0);
  private genTimer: ReturnType<typeof setInterval> | null = null;
  private genStartedAt = 0;
  /** 当前后台任务 ID 与轮询订阅 */
  currentTaskId = signal<string | null>(null);
  private taskPollSub: Subscription | null = null;

  // ---------- current preview / history ----------
  /** 「历史记录」Tab 内的预览项 */
  readonly historyPreview = signal<LessonPlanHistoryItem | null>(null);
  history = signal<LessonPlanHistoryItem[]>([]);
  private lastPreviewTaskId: string | null = null;

  readonly keyword = signal('');
  readonly filteredHistory = computed(() => {
    const kw = this.keyword().trim().toLowerCase();
    const list = this.history();
    if (!kw) return list;
    return list.filter(item =>
      (item.title || '').toLowerCase().includes(kw) ||
      (item.resourceName || '').toLowerCase().includes(kw),
    );
  });

  readonly pageIndex = signal(1);
  readonly pageSize = signal(8);
  readonly pagedHistory = computed(() => {
    const all = this.filteredHistory();
    const start = (this.pageIndex() - 1) * this.pageSize();
    return all.slice(start, start + this.pageSize());
  });

  readonly historyTabTitle = computed(() => `历史记录（${this.filteredHistory().length}）`);

  // 配置页 → 章节页 是否允许下一步：自动模式时资源必须可解析（仅核对资源就绪度，AI 解析会即时反馈失败），
  // 手动模式仅核对资源就绪度（章节页内允许空列表）。
  canNextConfig = computed(() =>
    this.resourceReady() && this.input().topic.trim().length > 0 && !this.isLoading() && !this.parsing()
  );

  canGenerateMulti = computed(() =>
    this.chapters().filter(c => c.title.trim().length > 0).length > 0 && !this.isLoading()
  );

  ngOnInit() {
    this.historyKey = this.computeHistoryKey();
    this.loadHistory();

    // 监听租户/登录状态变化：切换租户或重新登录时，重新加载对应桶里的历史记录，
    // 避免显示上一个租户的记录，也不会把新租户的记录写到旧 key 里。
    this.configState.createOnUpdateStream(() => true)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        const newKey = this.computeHistoryKey();
        if (newKey !== this.historyKey) {
          this.historyKey = newKey;
          this.historyPreview.set(null);
          this.genTab.set(0);
          this.history.set([]);
          this.loadHistory();
          this.syncBackendHistory();
        }
      });

    this.loadResources();
    // 后端已完成的任务合并进历史：直接浏览页面也能看到生成结果
    this.syncBackendHistory();

    // 任务完成实时合并：后台生成成功后，列表自动出现新数据
    this.aiTaskNotifications.completed$
      .pipe(takeUntil(this.destroy$))
      .subscribe(tasks => {
        for (const t of tasks) {
          if (t.taskType === AiTaskType.LessonPlanMulti) {
            if (this.mergeBackendTask(t, true)) {
              this.messageService.success('新教案已生成，已加入历史记录');
            }
          }
        }
      });

    // ?taskId= 深度链接（通知 / 任务中心跳转）：响应式订阅，页内跳转同样生效
    this.route.queryParamMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const taskId = params.get('taskId');
        if (taskId && taskId !== this.lastPreviewTaskId) {
          this.lastPreviewTaskId = taskId;
          this.loadTaskPreview(taskId);
        }
      });

    // 无深度链接时：恢复我名下正在跑的教案任务（切页回来也能看到生成中进度）
    if (!this.route.snapshot.queryParamMap.get('taskId')) {
      this.resumeRunningTask();
    }
  }

  ngOnDestroy() {
    this.cancelTaskPolling();
    this.stopGenTimer();
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ---------- history persistence ----------
  private loadHistory() {
    try {
      const raw = localStorage.getItem(this.historyKey);
      if (raw) {
        const parsed: LessonPlanHistoryItem[] = JSON.parse(raw);
        if (Array.isArray(parsed)) this.history.set(parsed);
      }
    } catch { /* ignore */ }
  }

  private saveHistory() {
    try {
      localStorage.setItem(this.historyKey, JSON.stringify(this.history().slice(0, this.MAX_HISTORY)));
    } catch { /* ignore */ }
  }

  // ---------- resources ----------
  private loadResources() {
    this.resourcesLoading.set(true);
    this.chatService.getResources()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.resources.set(data);
          this.resourcesLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to load resources:', err);
          this.resourcesLoading.set(false);
          this.messageService.error('加载资源列表失败');
        }
      });
  }

  // ---------- form helpers ----------
  updateTopic(value: string) {
    this.input.update(v => ({ ...v, topic: value }));
  }
  updateSubject(value: string) {
    this.input.update(v => ({ ...v, subject: value }));
  }
  updateGrade(value: string) {
    this.input.update(v => ({ ...v, grade: value }));
  }
  updateDuration(value: number) {
    this.input.update(v => ({ ...v, duration: value }));
  }
  updateCustomPrompt(value: string) {
    this.input.update(v => ({ ...v, customPrompt: value }));
  }

  setChapterSource(source: ChapterSource) {
    if (this.chapterSource() === source) return;
    this.chapterSource.set(source);
  }

  /** 文档列表状态图标 tooltip：全文索引 / AI 摘要情况 */
  resourceStatusTip(r: ResourceForChat): string {
    const idx = r.hasPageIndex ? '有全文索引' : '无全文索引';
    const sum = r.hasSummary ? '有摘要' : '无摘要';
    return `${idx} / ${sum}`;
  }

  /** 文档预览：点击文档行右侧眼睛图标，不触发选中。 */
  previewResource(event: Event, r: ResourceForChat): void {
    event.stopPropagation();
    if (!this.filePreview) return;
    const ext = (r.fileExtension || r.sourceFormat || '').replace('.', '');
    this.filePreview.open(r.id, r.name, ext, 0, true);
  }

  // ---------- workflow navigation ----------
  /**
   * 配置页 → 章节页：
   *  - 自动解析：调用 AI 即时解析文档章节，跳到章节页后可继续编辑/重新解析；
   *  - 手动填写：跳过 AI，直接进章节页，预填 3 个空白章节让用户填；
   * 任何阶段有正在跑的生成任务时都拒绝提交（按钮层已置灰，此处兜底）。
   */
  goNext() {
    if (this.isLoading() || this.parsing()) return;
    if (this.phase() === 'config') {
      if (this.chapterSource() === 'auto') {
        this.parseChapters();
      } else {
        this.enterManualChapters();
      }
    } else if (this.phase() === 'chapters') {
      this.startMultiGeneration();
    }
  }

  goBack() {
    if (this.phase() === 'chapters') {
      this.phase.set('config');
    }
  }

  resetWorkflow() {
    this.selectedResourceId.set(null);
    this.input.set({ topic: '', subject: '', grade: '', duration: 45, customPrompt: '' });
    this.resourceFilter.set('');
    this.chapters.set([]);
    this.chapterFilter.set('');
    this.expandedKeys.set([]);
    this.flashKey.set(null);
    this.parseRaw.set('');
    this.courseTitleHint.set('');
    this.multiResult.set(null);
    this.rawJson.set('');
    this.genError.set('');
    this.progress.set(0);
    this.progressMessage.set('');
    this.activeChapter.set(0);
    this.chapterTotal.set(0);
    this.phase.set('config');
    this.chapterSource.set('auto');
  }

  // ---------- chapter parsing (auto) ----------
  /** 自动解析：调用 AI 从文档中识别章节，结束后进入章节页供用户确认/编辑。 */
  parseChapters() {
    const resource = this.selectedResource();
    if (!resource) return;

    this.chapterSource.set('auto');
    this.phase.set('chapters');
    this.parsing.set(true);
    this.parseRaw.set('');
    this.chapters.set([]);
    this.chapterFilter.set('');
    this.expandedKeys.set([]);
    this.flashKey.set(null);
    this.genError.set('');

    let acc = '';
    let errorMessage = '';

    this.chatService.parseChapters({
      resourceId: resource.id,
      customPrompt: this.input().customPrompt?.trim() || undefined
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (evt: LessonPlanStreamEvent) => {
          if (evt.content) {
            acc += evt.content;
            this.parseRaw.set(acc);
          }
          if (evt.isError && evt.message) {
            errorMessage = evt.message;
          }
        },
        error: (err) => {
          console.error('Error parsing chapters:', err);
          this.parsing.set(false);
          this.phase.set('config');
          this.messageService.error('章节解析失败，请稍后重试');
        },
        complete: () => {
          this.parsing.set(false);
          if (errorMessage) {
            this.phase.set('config');
            this.messageService.error(errorMessage);
            return;
          }
          const parsed = this.extractJson(acc);
          const list: LessonPlanChapter[] = parsed?.chapters ?? [];
          if (list.length > 0) {
            this.courseTitleHint.set(parsed?.courseTitle || '');
            this.chapters.set(list.map((c, i) => ({
              key: this.uid(),
              order: i + 1,
              title: c.title || `第 ${i + 1} 章`,
              summary: c.summary || ''
            })));
            this.messageService.success(`已解析出 ${list.length} 个章节，请确认或修改`);
          } else {
            this.messageService.warning('未能解析出章节，可手动添加章节');
            this.chapters.set([]);
          }
        }
      });
  }

  /** 手动填写：跳过 AI 解析，预填 3 行空白章节，全部折叠态，只显示标题，需要时再展开填要点。 */
  enterManualChapters() {
    this.chapterSource.set('manual');
    this.phase.set('chapters');
    this.parsing.set(false);
    this.parseRaw.set('');
    this.courseTitleHint.set('');
    this.chapterFilter.set('');
    this.genError.set('');

    // 预填 3 行空白章节。折叠态只显示标题行，点 +/- 展开后才显示要点编辑区。
    const blanks: (LessonPlanChapter & { key: string })[] = [1, 2, 3].map(i => ({
      key: this.uid(),
      order: i,
      title: '',
      summary: '',
    }));
    this.chapters.set(blanks);
    this.expandedKeys.set([]);
    // 第一个章节高亮并聚焦其标题输入框，引导用户从顶部开始填（标题始终在主行可见，不受折叠影响）
    this.flashKey.set(blanks[0].key);
    setTimeout(() => {
      const row = document.querySelector(`tr.chapter-row[data-key="${blanks[0].key}"]`) as HTMLElement | null;
      (row?.querySelector('input') as HTMLElement | null)?.focus?.();
      setTimeout(() => {
        if (this.flashKey() === blanks[0].key) this.flashKey.set(null);
      }, 1600);
    });
  }

  /** 章节页：用于自动模式「重新解析」按钮，行为等同 parseChapters()。 */
  reparseChapters() {
    this.parseChapters();
  }

  addChapter() {
    const key = this.uid();
    const order = this.chapters().length + 1;
    this.chapters.update(list => [...list, { key, order, title: `第 ${order} 章`, summary: '' }]);
    // 清掉搜索关键词保证新行可见，并默认展开其要点编辑区
    this.chapterFilter.set('');
    this.expandedKeys.update(keys => [...keys, key]);
    this.flashKey.set(key);
    // 新增后滚动定位到新行，并聚焦其标题输入框
    setTimeout(() => {
      const row = document.querySelector(`tr.chapter-row[data-key="${key}"]`) as HTMLElement | null;
      row?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      setTimeout(() => {
        (row?.querySelector('input') as HTMLElement | null)?.focus?.();
      }, 400);
      setTimeout(() => {
        if (this.flashKey() === key) this.flashKey.set(null);
      }, 1600);
    });
  }

  removeChapterByKey(key: string) {
    this.chapters.update(list => list.filter(c => c.key !== key).map((c, i) => ({ ...c, order: i + 1 })));
    this.expandedKeys.update(keys => keys.filter(k => k !== key));
    if (this.flashKey() === key) this.flashKey.set(null);
  }

  isExpanded(key: string): boolean {
    return this.expandedKeys().includes(key);
  }

  setExpand(key: string, expand: boolean) {
    this.expandedKeys.update(keys =>
      expand ? (keys.includes(key) ? keys : [...keys, key]) : keys.filter(k => k !== key)
    );
  }

  toggleAllChapters() {
    if (this.allExpanded()) {
      this.expandedKeys.set([]);
    } else {
      this.expandedKeys.set(this.filteredChapters().map(c => c.key));
    }
  }

  moveChapter(index: number, direction: -1 | 1) {
    const target = index + direction;
    this.chapters.update(list => {
      if (target < 0 || target >= list.length) return list;
      const copy = [...list];
      [copy[index], copy[target]] = [copy[target], copy[index]];
      return copy.map((c, i) => ({ ...c, order: i + 1 }));
    });
  }

  updateChapterTitle(index: number, value: string) {
    this.chapters.update(list => list.map((c, i) => i === index ? { ...c, title: value } : c));
  }

  updateChapterSummary(index: number, value: string) {
    this.chapters.update(list => list.map((c, i) => i === index ? { ...c, summary: value } : c));
  }

  // ---------- multi generation ----------
  private startMultiGeneration() {
    // 单任务排队：一次只能生成一个教案
    if (this.isLoading()) return;
    const resource = this.selectedResource();
    if (!resource) return;

    const chapters = this.chapters()
      .filter(c => c.title.trim().length > 0)
      .map((c, i) => ({ order: i + 1, title: c.title.trim(), summary: (c.summary || '').trim() }));

    if (chapters.length === 0) {
      this.messageService.warning('请至少保留一个有效章节');
      return;
    }

    this.phase.set('generating');
    this.isLoading.set(true);
    this.multiResult.set(null);
    this.rawJson.set('');
    this.genError.set('');
    this.progress.set(0);
    this.progressMessage.set('任务已提交，正在后台生成…');
    this.activeChapter.set(0);
    this.chapterTotal.set(chapters.length);
    this.genElapsed.set(0);
    this.startGenTimer();

    const form = this.input();
    const payload = {
      resourceId: resource.id,
      topic: form.topic,
      subject: form.subject || undefined,
      grade: form.grade || undefined,
      duration: form.duration,
      customPrompt: form.customPrompt?.trim() || undefined,
      chapters
    };

    this.submitTask(AiTaskType.LessonPlanMulti, resource, form.topic || resource.name, payload, (resultJson) => {
      this.handleMultiCompleted(resource, resultJson);
    });
  }

  /** 多章节后台任务完成（正常提交 / 切页恢复共用）：只通知+进历史，不展示独立结果页。 */
  private handleMultiCompleted(resource: ResourceForChat, resultJson: string): void {
    this.stopGenTimer();
    this.isLoading.set(false);
    this.rawJson.set(resultJson);
    const parsed = this.extractJson(resultJson) as MultiChapterPlan | null;
    if (parsed && Array.isArray(parsed.chapters) && parsed.chapters.length > 0) {
      this.multiResult.set(parsed);
      this.finishMulti(resource, parsed, resultJson);
    } else {
      this.phase.set('chapters');
      this.messageService.warning('整体教案结果解析失败，请重试');
    }
  }

  /** 用户在生成中途点"取消生成"：取消后台任务并回到章节页。 */
  cancelMultiGeneration() {
    if (!this.isLoading()) return;
    const id = this.currentTaskId();
    if (id) {
      this.aiTaskService.cancel(id).subscribe({ next: () => {}, error: () => {} });
    }
    this.cancelTaskPolling();
    this.stopGenTimer();
    this.isLoading.set(false);
    this.phase.set('chapters');
    this.messageService.info('已取消生成');
  }

  // ---------- background task helpers ----------
  private submitTask(
    taskType: AiTaskType,
    resource: ResourceForChat,
    title: string,
    payload: unknown,
    onCompleted: (resultJson: string) => void,
  ) {
    this.cancelTaskPolling();
    this.aiTaskService
      .create({
        taskType,
        title: title || 'AI 生成任务',
        resourceId: resource.id,
        resourceName: resource.name,
        inputJson: JSON.stringify(payload),
      })
      .subscribe({
        next: (task) => {
          this.currentTaskId.set(task.id);
          this.messageService.success('任务已提交后台生成，可切换页面，完成后会通知你');
          this.followTask(task.id, onCompleted);
        },
        error: (err) => {
          this.isLoading.set(false);
          this.phase.set('chapters');
          this.messageService.error(err?.error?.error?.message || '提交任务失败，请重试');
        },
      });
  }

  private followTask(taskId: string, onCompleted: (resultJson: string) => void) {
    this.cancelTaskPolling();
    this.taskPollSub = this.aiTaskNotifications
      .pollTask(taskId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (t) => {
          if (t.status === AiTaskStatus.Pending || t.status === AiTaskStatus.Running) {
            if (typeof t.progress === 'number') this.progress.set(t.progress);
            if (t.progressMessage) this.progressMessage.set(t.progressMessage);
          } else if (t.status === AiTaskStatus.Completed) {
            this.cancelTaskPolling();
            onCompleted(t.resultJson || '');
          } else {
            this.cancelTaskPolling();
            this.isLoading.set(false);
            this.stopGenTimer();
            this.phase.set('chapters');
            this.messageService.error(t.errorMessage || '生成失败，请重试');
          }
        },
        error: () => {
          this.cancelTaskPolling();
          this.isLoading.set(false);
          this.stopGenTimer();
          this.phase.set('chapters');
          this.messageService.error('生成失败，请稍后重试');
        },
      });
  }

  private cancelTaskPolling() {
    this.taskPollSub?.unsubscribe();
    this.taskPollSub = null;
  }

  /**
   * 恢复进行中的任务：用户中途切走再回来，生成中面板继续显示进度。
   * （切页时轮询已随组件销毁，这里按我名下最新的 Pending/Running 任务重建跟进。）
   * 只恢复多章节任务（单章节方案已下线）。
   */
  private resumeRunningTask(): void {
    if (this.isLoading()) return;
    this.aiTaskService
      .getList({ taskType: AiTaskType.LessonPlanMulti, onlyMine: true, maxResultCount: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          const running = (res.items || [])
            .filter((t) =>
              (t.status === AiTaskStatus.Pending || t.status === AiTaskStatus.Running))
            .sort((a, b) => +new Date(b.creationTime) - +new Date(a.creationTime))[0];
          if (!running || this.isLoading()) return;
          this.followResumedTask(running);
        },
      });
  }

  /** 跟进恢复的任务：还原章节列表与进度面板，完成走统一的完成流程。 */
  private followResumedTask(task: AiGenerationTaskDto): void {
    this.phase.set('generating');
    this.genTab.set(0);
    this.isLoading.set(true);
    this.currentTaskId.set(task.id);
    this.progress.set(task.progress ?? 0);
    this.progressMessage.set(task.progressMessage || '后台生成中…');
    const resource: ResourceForChat = this.resources().find((r) => r.id === task.resourceId) ?? {
      id: task.resourceId || '',
      name: task.resourceName || '资源',
      nodeCount: 0,
    };
    const chapters = this.parseChaptersFromInput(task.inputJson);
    if (chapters.length > 0) {
      this.chapters.set(chapters);
      this.chapterTotal.set(chapters.length);
    }
    this.genElapsed.set(0);
    this.startGenTimer();
    this.followTask(task.id, (json) => this.handleMultiCompleted(resource, json));
  }

  /** 从任务 inputJson 还原章节列表（供恢复生成中面板展示）。 */
  private parseChaptersFromInput(inputJson?: string): (LessonPlanChapter & { key: string })[] {
    if (!inputJson) return [];
    try {
      const input = JSON.parse(inputJson) as { chapters?: { order?: number; title?: string; summary?: string }[] };
      const list = Array.isArray(input.chapters) ? input.chapters : [];
      return list
        .filter((c) => c && typeof c.title === 'string' && c.title.trim().length > 0)
        .map((c, i) => ({
          key: this.uid(),
          order: c.order || i + 1,
          title: c.title!.trim(),
          summary: (c.summary || '').trim(),
        }));
    } catch {
      return [];
    }
  }

  private loadTaskPreview(taskId: string) {
    this.aiTaskService
      .get(taskId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (task) => this.openTaskResult(task),
        error: () => this.messageService.error('加载任务结果失败'),
      });
  }

  private openTaskResult(task: AiGenerationTaskDto) {
    if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
      this.isLoading.set(true);
      this.phase.set('generating');
      this.genTab.set(0);
      this.progressMessage.set(task.progressMessage || '后台生成中…');
      this.currentTaskId.set(task.id);
      this.followTask(task.id, (json) => {
        this.isLoading.set(false);
        this.previewTask({ ...task, status: AiTaskStatus.Completed, resultJson: json });
      });
      return;
    }
    this.previewTask(task);
  }

  /**
   * ?taskId= / 后台任务详情 → 合并进历史并在「历史记录」Tab 的结果 UI 中展示。
   */
  private previewTask(task: AiGenerationTaskDto) {
    const item = this.buildHistoryItemFromTask(task);
    if (!item) {
      this.messageService.warning('任务结果解析失败');
      return;
    }
    this.history.update((list) => (list.some((x) => x.id === item.id) ? list : [item, ...list].slice(0, this.MAX_HISTORY)));
    this.saveHistory();
    this.historyPreview.set(item);
    this.genTab.set(1);
  }

  /** 后端任务 DTO → 历史记录项，解析失败返回 null。 */
  private buildHistoryItemFromTask(task: AiGenerationTaskDto): LessonPlanHistoryItem | null {
    if (task.status !== AiTaskStatus.Completed || !task.resultJson) return null;
    const json = task.resultJson;
    const resource: ResourceForChat = this.resources().find((r) => r.id === task.resourceId) ?? {
      id: task.resourceId || '',
      name: task.resourceName || '资源',
      nodeCount: 0,
    };

    const parsed = this.extractJson(json) as MultiChapterPlan | null;
    if (!parsed || !Array.isArray(parsed.chapters) || parsed.chapters.length === 0) return null;

    return {
      id: task.id,
      title: parsed.courseTitle || task.title,
      subject: parsed.subject || '-',
      grade: parsed.grade || '-',
      duration: parsed.duration || 0,
      chapterCount: parsed.chapters.length,
      resourceName: resource.name,
      resourceId: resource.id,
      createdAt: task.completedAt || task.creationTime,
      multiResult: parsed,
      rawJson: json,
    };
  }

  /**
   * 把后端已完成的教案任务合并进历史（去重、有上限、持久化）。
   * @returns 是否新增了一条记录
   */
  private mergeBackendTask(task: AiGenerationTaskDto, fetchFullIfNeeded = false): boolean {
    if (this.history().some((x) => x.id === task.id)) return false;
    if (!task.resultJson && fetchFullIfNeeded) {
      // 列表接口不返回 ResultJson 大字段，取详情后再合并
      this.aiTaskService
        .get(task.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (full) => {
            if (this.mergeBackendTask(full)) {
              this.messageService.success('新教案已生成，已加入历史记录');
            }
          },
        });
      return false;
    }
    const item = this.buildHistoryItemFromTask(task);
    if (!item) return false;
    this.history.update((list) => [item, ...list].slice(0, this.MAX_HISTORY));
    this.saveHistory();
    return true;
  }

  /**
   * 进页即同步：拉取后端已完成的多章节教案任务并入历史，
   * 直接浏览页面也能看到（含其他浏览器提交的）生成结果。
   */
  private syncBackendHistory(): void {
    this.aiTaskService
      .getList({ taskType: AiTaskType.LessonPlanMulti, status: AiTaskStatus.Completed, onlyMine: true, maxResultCount: 20 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          const tasks = (res.items || []);
          const missing = tasks.filter((t) => !this.history().some((x) => x.id === t.id)).slice(0, 10);
          for (const t of missing) {
            this.mergeBackendTask(t, true);
          }
        },
      });
  }

  /** 已用时 MM:SS，供模板展示。 */
  genElapsedText = computed(() => {
    const s = this.genElapsed();
    const m = Math.floor(s / 60);
    return `${String(m).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  });

  private startGenTimer() {
    this.stopGenTimer();
    this.genStartedAt = Date.now();
    this.genTimer = setInterval(() => {
      this.genElapsed.set(Math.floor((Date.now() - this.genStartedAt) / 1000));
    }, 1000);
  }

  private stopGenTimer() {
    if (this.genTimer !== null) {
      clearInterval(this.genTimer);
      this.genTimer = null;
    }
  }

  private finishMulti(resource: ResourceForChat, parsed: MultiChapterPlan, json: string) {
    const form = this.input();
    const item: LessonPlanHistoryItem = {
      id: this.uid(),
      title: parsed.courseTitle || form.topic || '多章节教案',
      subject: parsed.subject || form.subject || '-',
      grade: parsed.grade || form.grade || '-',
      duration: parsed.duration || form.duration * parsed.chapters.length,
      chapterCount: parsed.chapters.length,
      resourceName: resource.name,
      resourceId: resource.id,
      createdAt: new Date().toISOString(),
      multiResult: parsed,
      rawJson: json
    };
    this.commitItem(item);
  }

  /**
   * 生成完成：只通知 + 合并进历史，不展示独立结果页，
   * 直接回到「历史记录」Tab（新教案在列表顶部，可点预览 / 下载）。
   */
  private commitItem(item: LessonPlanHistoryItem) {
    this.history.update(list => [item, ...list].slice(0, this.MAX_HISTORY));
    this.saveHistory();
    this.phase.set('config');
    this.genTab.set(1);
    this.pageIndex.set(1);
    this.messageService.success('整体教案已生成');
  }

  // ---------- preview / table actions ----------
  previewHistory(item: LessonPlanHistoryItem) {
    this.historyPreview.set(item);
    this.genTab.set(1);
  }

  backToList() {
    this.historyPreview.set(null);
  }

  async downloadHistory(item: LessonPlanHistoryItem) {
    if (!item?.rawJson) return;
    this.isExporting.set(true);
    try {
      const blob = await this.chatService.exportMultiChapterLessonPlanDocx(item.rawJson);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${item.title || '教案'}_${item.createdAt.slice(0, 10)}.docx`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      this.messageService.success('教案已下载');
    } catch (err) {
      console.error('Failed to export docx:', err);
      this.messageService.error('导出失败，请重试');
    } finally {
      this.isExporting.set(false);
    }
  }

  removeHistory(item: LessonPlanHistoryItem, event?: MouseEvent) {
    event?.stopPropagation();
    this.history.update(list => list.filter(x => x.id !== item.id));
    this.saveHistory();
    if (this.historyPreview()?.id === item.id) {
      this.historyPreview.set(null);
    }
    this.messageService.success('已删除');
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
  }

  reload(): void {
    this.pageIndex.set(1);
  }

  resetSearch(): void {
    this.keyword.set('');
    this.reload();
  }

  formatDate(iso: string): string {
    try {
      const d = new Date(iso);
      return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
    } catch { return iso; }
  }

  chapterState(order: number): 'done' | 'active' | 'pending' {
    const active = this.activeChapter();
    if (active <= 0) return 'pending';
    if (order < active) return 'done';
    if (order === active) return 'active';
    return 'pending';
  }

  // ---------- shared helpers ----------
  private uid(): string {
    return Date.now().toString(36) + Math.random().toString(36).slice(2, 6);
  }

  private extractJson(raw: string): any | null {
    if (!raw) return null;
    let text = raw.trim();
    if (text.startsWith('```')) {
      const firstNewline = text.indexOf('\n');
      if (firstNewline >= 0) text = text.substring(firstNewline + 1);
      if (text.endsWith('```')) text = text.substring(0, text.length - 3).trimEnd();
    }
    const start = text.indexOf('{');
    const end = text.lastIndexOf('}');
    if (start >= 0 && end > start) {
      text = text.substring(start, end - start + 1);
    }
    try {
      return JSON.parse(text);
    } catch {
      return null;
    }
  }
}
