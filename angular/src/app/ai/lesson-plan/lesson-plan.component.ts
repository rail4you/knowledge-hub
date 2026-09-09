import { Component, signal, inject, computed, ChangeDetectionStrategy, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subject, takeUntil } from 'rxjs';
import { ChatService, ResourceForChat } from '../services/chat.service';

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

interface LessonPlanHistoryItem {
  id: string;
  title: string;
  subject: string;
  grade: string;
  duration: number;
  resourceName: string;
  resourceId: string;
  createdAt: string;
  result: LessonPlanResult;
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
    NzModalModule,
    NzCollapseModule,
    NzDescriptionsModule,
    NzTagModule,
    NzSpinModule,
    NzIconModule,
    NzEmptyModule,
    NzTableModule,
    NzPopconfirmModule,
    NzTooltipModule
  ],
  templateUrl: './lesson-plan.component.html',
  styleUrls: ['./lesson-plan.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LessonPlanComponent implements OnInit, OnDestroy {
  private readonly chatService = inject(ChatService);
  private readonly messageService = inject(NzMessageService);
  private readonly destroy$ = new Subject<void>();
  private readonly HISTORY_KEY = 'kh-lesson-plan-history';

  // resources
  resources = signal<ResourceForChat[]>([]);
  resourcesLoading = signal(false);
  selectedResourceId = signal<string | null>(null);
  resourceFilter = signal('');

  selectedResource = computed(() => {
    const id = this.selectedResourceId();
    if (!id) return null;
    return this.resources().find(r => r.id === id) ?? null;
  });

  filteredResources = computed(() => {
    const kw = this.resourceFilter().trim().toLowerCase();
    const list = this.resources().filter(r => r.hasSummary === true);
    if (!kw) return list;
    return list.filter(r => r.name.toLowerCase().includes(kw) || (r.sourceFormat ?? '').toLowerCase().includes(kw));
  });

  // form input
  input = signal<LessonPlanInput>({
    topic: '',
    subject: '',
    grade: '',
    duration: 45,
    customPrompt: ''
  });

  // ui state per spec
  formModalVisible = signal(false);
  viewMode = signal<'list' | 'preview'>('list');

  // generation
  result = signal<LessonPlanResult | null>(null);
  rawJson = signal('');
  isLoading = signal(false);
  isExporting = signal(false);

  // history table
  history = signal<LessonPlanHistoryItem[]>([]);
  previewItem = signal<LessonPlanHistoryItem | null>(null);

  // 搜索关键字 + 过滤后的列表（与双高表格一致）
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

  // 前端分页：基于 filteredHistory 切片
  readonly pageIndex = signal(1);
  readonly pageSize = signal(8);
  readonly pagedHistory = computed(() => {
    const all = this.filteredHistory();
    const start = (this.pageIndex() - 1) * this.pageSize();
    return all.slice(start, start + this.pageSize());
  });

  canGenerate = computed(() => {
    const i = this.input();
    const r = this.selectedResource();
    return !!r && r.hasSummary === true && i.topic.trim().length > 0 && !this.isLoading();
  });

  ngOnInit() {
    this.loadHistory();
    this.loadResources();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ---------- history persistence ----------
  private loadHistory() {
    try {
      const raw = localStorage.getItem(this.HISTORY_KEY);
      if (raw) {
        const parsed: LessonPlanHistoryItem[] = JSON.parse(raw);
        if (Array.isArray(parsed)) this.history.set(parsed);
      }
    } catch { /* ignore */ }
  }

  private saveHistory() {
    try {
      localStorage.setItem(this.HISTORY_KEY, JSON.stringify(this.history().slice(0, 50)));
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

  toggleForm() {
    this.formModalVisible.update(v => !v);
  }
  openForm() {
    this.formModalVisible.set(true);
  }
  cancelForm() {
    this.formModalVisible.set(false);
  }

  // ---------- generate ----------
  generate() {
    const input = this.input();
    const resource = this.selectedResource();
    if (!resource || !resource.hasSummary || !input.topic.trim()) return;

    this.isLoading.set(true);
    this.result.set(null);
    this.rawJson.set('');

    let fullResponse = '';

    this.chatService.generateLessonPlan({
      resourceId: resource.id,
      topic: input.topic,
      subject: input.subject || undefined,
      grade: input.grade || undefined,
      duration: input.duration,
      customPrompt: input.customPrompt?.trim() || undefined
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (chunk) => {
          if (chunk.content) {
            fullResponse += chunk.content;
            this.rawJson.set(fullResponse);
            this.tryParseResult(fullResponse);
          }
        },
        error: (err) => {
          console.error('Error generating lesson plan:', err);
          this.isLoading.set(false);
          this.messageService.error('教案生成失败，请稍后重试');
        },
        complete: () => {
          this.isLoading.set(false);
          if (fullResponse && !this.result()) {
            this.tryParseResult(fullResponse, true);
          }
          const parsed = this.result();
          const json = this.rawJson();
          if (parsed && json) {
            const item: LessonPlanHistoryItem = {
              id: Date.now().toString(36) + Math.random().toString(36).slice(2, 6),
              title: parsed.title || input.topic || '未命名教案',
              subject: parsed.subject || input.subject || '-',
              grade: parsed.grade || input.grade || '-',
              duration: parsed.duration || input.duration,
              resourceName: resource.name,
              resourceId: resource.id,
              createdAt: new Date().toISOString(),
              result: parsed,
              rawJson: json
            };
            this.history.update(list => [item, ...list].slice(0, 50));
            this.saveHistory();
            this.previewItem.set(item);
            this.viewMode.set('preview');
            this.formModalVisible.set(false);
            this.messageService.success('教案已生成');
          } else if (fullResponse) {
            // parsing failed but still show preview with raw
            this.messageService.warning('AI 返回的数据格式不完整，已保存原始内容，请重试或检查预览');
          }
        }
      });
  }

  private tryParseResult(json: string, final = false) {
    try {
      let cleanJson = json.trim();
      if (cleanJson.startsWith('```')) {
        const firstNewline = cleanJson.indexOf('\n');
        if (firstNewline >= 0) cleanJson = cleanJson.substring(firstNewline + 1);
        if (cleanJson.endsWith('```')) {
          cleanJson = cleanJson.substring(0, cleanJson.length - 3).trimEnd();
        }
      }
      const parsed = JSON.parse(cleanJson);
      this.result.set(parsed);
    } catch {
      if (final) {
        this.messageService.warning('AI 返回的数据格式不完整，请重新生成');
      }
    }
  }

  // ---------- preview / table actions ----------
  previewHistory(item: LessonPlanHistoryItem) {
    this.previewItem.set(item);
    this.viewMode.set('preview');
  }

  backToList() {
    this.viewMode.set('list');
  }

  async downloadHistory(item: LessonPlanHistoryItem) {
    if (!item.rawJson) return;
    this.isExporting.set(true);
    try {
      const blob = await this.chatService.exportLessonPlanDocx(item.rawJson);
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

  async downloadPreview() {
    const item = this.previewItem();
    if (item) {
      await this.downloadHistory(item);
      return;
    }
    // fallback: current rawJson (streaming before saved)
    const json = this.rawJson();
    if (!json) return;
    this.isExporting.set(true);
    try {
      const blob = await this.chatService.exportLessonPlanDocx(json);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `教案_${new Date().toISOString().slice(0, 10)}.docx`;
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
    if (this.previewItem()?.id === item.id) {
      this.previewItem.set(null);
      this.viewMode.set('list');
    }
    this.messageService.success('已删除');
  }

  // 分页：仅切页/切大小时同步信号
  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
  }

  // 搜索：reset 时回到第一页
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

  resetForm() {
    this.selectedResourceId.set(null);
    this.input.set({ topic: '', subject: '', grade: '', duration: 45, customPrompt: '' });
    this.result.set(null);
    this.rawJson.set('');
    this.resourceFilter.set('');
  }

  // keep legacy reset for template compat if needed
  reset() {
    this.resetForm();
  }
}
