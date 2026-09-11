import { Component, signal, inject, computed, ChangeDetectionStrategy, OnInit, OnDestroy, ViewChild, ElementRef } from '@angular/core';
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
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subject, Subscription, takeUntil } from 'rxjs';
import { ConfigStateService } from '@abp/ng.core';
import {
  ChatService,
  ResourceForChat,
  LessonPlanChapter,
  LessonPlanStreamEvent
} from '../services/chat.service';

interface LessonPlanInput {
  topic: string;
  subject: string;
  grade: string;
  duration: number;
  customPrompt: string;
}

interface TeachingSection {
  name: string;
  duration: number;
  content: string;
  activities: string[];
}

interface LessonPlanResult {
  title: string;
  subject: string;
  grade: string;
  duration: number;
  objectives: string[];
  keyPoints: string[];
  difficulties: string[];
  sections: TeachingSection[];
  methods: string[];
  resources: string[];
  assessment: string[];
  homework: string[];
}

interface MultiChapterPlan {
  courseTitle: string;
  subject: string;
  grade: string;
  duration: number;
  courseObjectives: string[];
  chapters: { order: number; chapterTitle: string; lessonPlan: LessonPlanResult }[];
}

type WorkflowMode = 'single' | 'multi';
type Phase = 'config' | 'chapters' | 'generating' | 'result';
type ViewMode = 'list' | 'workflow' | 'preview';

interface LessonPlanHistoryItem {
  id: string;
  mode: WorkflowMode;
  title: string;
  subject: string;
  grade: string;
  duration: number;
  chapterCount?: number;
  resourceName: string;
  resourceId: string;
  createdAt: string;
  singleResult?: LessonPlanResult;
  multiResult?: MultiChapterPlan;
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
    NzStepsModule
  ],
  templateUrl: './lesson-plan.component.html',
  styleUrls: ['./lesson-plan.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LessonPlanComponent implements OnInit, OnDestroy {
  private readonly chatService = inject(ChatService);
  private readonly messageService = inject(NzMessageService);
  private readonly configState = inject(ConfigStateService);
  private readonly destroy$ = new Subject<void>();
  @ViewChild('chapterList') chapterListRef?: ElementRef<HTMLElement>;
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
    const multi = this.workflowMode() === 'multi';
    return this.resources().filter(r =>
      multi ? (r.hasSummary === true || r.hasPageIndex === true) : r.hasSummary === true
    );
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
    return this.workflowMode() === 'single'
      ? r.hasSummary === true
      : (r.hasSummary === true || r.hasPageIndex === true);
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
  workflowMode = signal<WorkflowMode>('single');
  phase = signal<Phase>('config');
  viewMode = signal<ViewMode>('list');

  // steps
  steps = computed(() =>
    this.workflowMode() === 'single'
      ? ['配置', '生成教案', '完成']
      : ['配置', '解析章节', '生成教案', '完成']
  );

  stepIndex = computed(() => {
    const phase = this.phase();
    if (this.workflowMode() === 'single') {
      return phase === 'config' ? 0 : phase === 'generating' ? 1 : 2;
    }
    return phase === 'config' ? 0 : phase === 'chapters' ? 1 : phase === 'generating' ? 2 : 3;
  });

  // ---------- chapter parsing ----------
  chapters = signal<LessonPlanChapter[]>([]);
  courseTitleHint = signal('');
  parsing = signal(false);
  parseRaw = signal('');

  // ---------- generation ----------
  result = signal<LessonPlanResult | null>(null);
  multiResult = signal<MultiChapterPlan | null>(null);
  rawJson = signal('');
  isLoading = signal(false);
  isExporting = signal(false);
  singleError = signal('');
  genError = signal('');

  progress = signal(0);
  progressMessage = signal('');
  activeChapter = signal(0);
  chapterTotal = signal(0);
  // 多章节生成：已用时（秒）+ 取消订阅句柄。单章大模型调用常需 30~120 秒，
  // 用已用时 + 后端 5 秒心跳让用户感知进度，而不是静止不动看似“卡死”。
  genElapsed = signal(0);
  private genTimer: ReturnType<typeof setInterval> | null = null;
  private genStartedAt = 0;
  private multiGenSub: Subscription | null = null;
  private cancelRequested = false;

  // ---------- current preview / history ----------
  activeItem = signal<LessonPlanHistoryItem | null>(null);
  history = signal<LessonPlanHistoryItem[]>([]);

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

  canNextConfig = computed(() => {
    const i = this.input();
    return this.resourceReady() && i.topic.trim().length > 0 && !this.isLoading() && !this.parsing();
  });

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
          this.activeItem.set(null);
          if (this.viewMode() === 'preview') this.viewMode.set('list');
          this.history.set([]);
          this.loadHistory();
        }
      });

    this.loadResources();
  }

  ngOnDestroy() {
    this.multiGenSub?.unsubscribe();
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

  // ---------- workflow navigation ----------
  openWorkflow() {
    this.resetWorkflow();
    this.viewMode.set('workflow');
  }

  closeWorkflow() {
    this.viewMode.set('list');
  }

  setMode(mode: WorkflowMode) {
    if (this.workflowMode() === mode) return;
    this.workflowMode.set(mode);
    this.selectedResourceId.set(null);
    this.resourceFilter.set('');
    this.chapters.set([]);
    this.parseRaw.set('');
    this.phase.set('config');
  }

  goNext() {
    if (this.phase() === 'config') {
      if (this.workflowMode() === 'single') {
        this.startSingleGeneration();
      } else {
        this.parseChapters();
      }
    } else if (this.phase() === 'chapters') {
      this.startMultiGeneration();
    }
  }

  goBack() {
    if (this.phase() === 'chapters' || this.phase() === 'result') {
      this.phase.set('config');
    }
  }

  resetWorkflow() {
    this.selectedResourceId.set(null);
    this.input.set({ topic: '', subject: '', grade: '', duration: 45, customPrompt: '' });
    this.resourceFilter.set('');
    this.chapters.set([]);
    this.parseRaw.set('');
    this.courseTitleHint.set('');
    this.result.set(null);
    this.multiResult.set(null);
    this.rawJson.set('');
    this.singleError.set('');
    this.genError.set('');
    this.progress.set(0);
    this.progressMessage.set('');
    this.activeChapter.set(0);
    this.chapterTotal.set(0);
    this.activeItem.set(null);
    this.phase.set('config');
  }

  // ---------- chapter parsing (multi) ----------
  parseChapters() {
    const resource = this.selectedResource();
    if (!resource) return;

    this.phase.set('chapters');
    this.parsing.set(true);
    this.parseRaw.set('');
    this.chapters.set([]);
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
              order: i + 1,
              title: c.title || `第 ${i + 1} 章`,
              summary: c.summary || ''
            })));
            this.messageService.success(`已解析出 ${list.length} 个章节，请确认或修改`);
          } else {
            this.messageService.warning('未能解析出章节，请重试或手动添加章节');
            this.chapters.set([]);
          }
        }
      });
  }

  addChapter() {
    this.chapters.update(list => [
      ...list,
      { order: list.length + 1, title: `第 ${list.length + 1} 章`, summary: '' }
    ]);
    // 新增后滚动定位到新章节，并聚焦其标题输入框
    setTimeout(() => {
      const el = this.chapterListRef?.nativeElement
        ?? document.querySelector('.chapter-edit-list') as HTMLElement | null;
      if (!el) return;
      el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' });
      const rows = el.querySelectorAll('.chapter-edit-row');
      const lastRow = rows[rows.length - 1] as HTMLElement | undefined;
      if (lastRow) {
        lastRow.classList.add('flash');
        setTimeout(() => lastRow.classList.remove('flash'), 1600);
        const input = lastRow.querySelector('input') as HTMLElement | null;
        // 等滚动基本完成后再 focus，避免被滚动打断
        setTimeout(() => input?.focus?.(), 350);
      }
    });
  }

  removeChapter(index: number) {
    this.chapters.update(list => list.filter((_, i) => i !== index).map((c, i) => ({ ...c, order: i + 1 })));
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

  // ---------- single generation ----------
  private startSingleGeneration() {
    const resource = this.selectedResource();
    if (!resource) return;

    this.phase.set('generating');
    this.isLoading.set(true);
    this.result.set(null);
    this.rawJson.set('');
    this.singleError.set('');
    this.progress.set(0);
    this.progressMessage.set('正在生成教案…');

    let full = '';
    const form = this.input();

    this.chatService.generateLessonPlan({
      resourceId: resource.id,
      topic: form.topic,
      subject: form.subject || undefined,
      grade: form.grade || undefined,
      duration: form.duration,
      customPrompt: form.customPrompt?.trim() || undefined
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (chunk) => {
          if (chunk.content) {
            full += chunk.content;
            this.rawJson.set(full);
            this.tryParseResult(full);
          }
        },
        error: (err) => {
          console.error('Error generating lesson plan:', err);
          this.isLoading.set(false);
          this.phase.set('config');
          this.messageService.error('教案生成失败，请稍后重试');
        },
        complete: () => {
          this.isLoading.set(false);
          if (this.singleError()) {
            this.phase.set('config');
            this.messageService.error(this.singleError());
            return;
          }
          if (full && !this.result()) {
            this.tryParseResult(full, true);
          }
          const parsed = this.result();
          if (parsed) {
            this.finishSingle(resource, parsed, full);
          } else {
            this.phase.set('config');
            this.messageService.warning('AI 返回的数据格式不完整，请重新生成');
          }
        }
      });
  }

  private tryParseResult(json: string, final = false): boolean {
    const parsed = this.extractJson(json);
    if (parsed?.error) {
      this.singleError.set(String(parsed.error));
      return false;
    }
    if (parsed && (parsed.title !== undefined || Array.isArray(parsed.sections))) {
      this.result.set(parsed as LessonPlanResult);
      return true;
    }
    if (final) {
      this.messageService.warning('AI 返回的数据格式不完整，请重新生成');
    }
    return false;
  }

  private finishSingle(resource: ResourceForChat, parsed: LessonPlanResult, json: string) {
    const form = this.input();
    const item: LessonPlanHistoryItem = {
      id: this.uid(),
      mode: 'single',
      title: parsed.title || form.topic || '未命名教案',
      subject: parsed.subject || form.subject || '-',
      grade: parsed.grade || form.grade || '-',
      duration: parsed.duration || form.duration,
      resourceName: resource.name,
      resourceId: resource.id,
      createdAt: new Date().toISOString(),
      singleResult: parsed,
      rawJson: json
    };
    this.commitItem(item);
  }

  // ---------- multi generation ----------
  private startMultiGeneration() {
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
    this.progressMessage.set('正在准备生成…');
    this.activeChapter.set(0);
    this.chapterTotal.set(chapters.length);
    this.genElapsed.set(0);
    this.cancelRequested = false;
    this.startGenTimer();

    const form = this.input();

    this.multiGenSub?.unsubscribe();
    this.multiGenSub = this.chatService.generateMultiChapterLessonPlan({
      resourceId: resource.id,
      topic: form.topic,
      subject: form.subject || undefined,
      grade: form.grade || undefined,
      duration: form.duration,
      customPrompt: form.customPrompt?.trim() || undefined,
      chapters
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (evt: LessonPlanStreamEvent) => {
          if (typeof evt.progress === 'number') this.progress.set(evt.progress);
          if (evt.message) this.progressMessage.set(evt.message);
          if (evt.chapterTotal) this.chapterTotal.set(evt.chapterTotal);
          if (evt.chapterIndex) this.activeChapter.set(evt.chapterIndex);
          if (evt.isError && evt.message) this.genError.set(evt.message);
          if (evt.resultJson) this.rawJson.set(evt.resultJson);
        },
        error: (err) => {
          console.error('Error generating multi-chapter lesson plan:', err);
          this.stopGenTimer();
          this.multiGenSub = null;
          this.isLoading.set(false);
          this.phase.set('chapters');
          this.messageService.error('整体教案生成失败，请稍后重试');
        },
        complete: () => {
          this.stopGenTimer();
          this.multiGenSub = null;
          this.isLoading.set(false);
          // 用户主动取消：静默回到章节页，不弹“解析失败”。
          if (this.cancelRequested) {
            this.cancelRequested = false;
            this.phase.set('chapters');
            return;
          }
          if (this.genError()) {
            this.phase.set('chapters');
            this.messageService.error(this.genError());
            return;
          }
          const json = this.rawJson();
          const parsed = json ? this.extractJson(json) as MultiChapterPlan : null;
          if (parsed && Array.isArray(parsed.chapters) && parsed.chapters.length > 0) {
            this.multiResult.set(parsed);
            this.finishMulti(resource, parsed, json);
          } else {
            this.phase.set('chapters');
            this.messageService.warning('整体教案结果解析失败，请重试');
          }
        }
      });
  }

  /** 用户在生成中途点“取消生成”：abort 底层的 fetch，后端联动取消大模型调用。 */
  cancelMultiGeneration() {
    if (!this.isLoading()) return;
    this.cancelRequested = true;
    this.multiGenSub?.unsubscribe();
    this.multiGenSub = null;
    this.stopGenTimer();
    this.isLoading.set(false);
    this.phase.set('chapters');
    this.messageService.info('已取消生成');
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
      mode: 'multi',
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

  private commitItem(item: LessonPlanHistoryItem) {
    this.history.update(list => [item, ...list].slice(0, this.MAX_HISTORY));
    this.saveHistory();
    this.activeItem.set(item);
    this.phase.set('result');
    this.messageService.success(item.mode === 'multi' ? '整体教案已生成' : '教案已生成');
  }

  // ---------- preview / table actions ----------
  previewHistory(item: LessonPlanHistoryItem) {
    this.activeItem.set(item);
    this.viewMode.set('preview');
  }

  backToList() {
    this.viewMode.set('list');
  }

  async downloadActive() {
    const item = this.activeItem();
    if (!item?.rawJson) return;
    this.isExporting.set(true);
    try {
      const blob = item.mode === 'multi'
        ? await this.chatService.exportMultiChapterLessonPlanDocx(item.rawJson)
        : await this.chatService.exportLessonPlanDocx(item.rawJson);
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

  async downloadHistory(item: LessonPlanHistoryItem) {
    const previous = this.activeItem();
    this.activeItem.set(item);
    await this.downloadActive();
    this.activeItem.set(previous);
  }

  removeHistory(item: LessonPlanHistoryItem, event?: MouseEvent) {
    event?.stopPropagation();
    this.history.update(list => list.filter(x => x.id !== item.id));
    this.saveHistory();
    if (this.activeItem()?.id === item.id) {
      this.activeItem.set(null);
      if (this.viewMode() === 'preview') this.viewMode.set('list');
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
