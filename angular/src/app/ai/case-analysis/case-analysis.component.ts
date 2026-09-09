import { Component, signal, inject, computed, ChangeDetectionStrategy, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subject, takeUntil } from 'rxjs';
import { ChatService, ResourceForChat } from '../services/chat.service';

interface CaseAnalysisResult {
  title: string;
  summary: string;
  background: {
    industry: string;
    timeframe: string;
    context: string;
    stakeholders: string[];
  };
  keyIssues: {
    id: string;
    title: string;
    description: string;
    impact: string;
    severity: string;
  }[];
  solutions: {
    id: string;
    title: string;
    description: string;
    steps: string[];
    expectedOutcome: string;
  }[];
  keyInsights: string[];
  recommendations: string[];
}

interface CaseAnalysisHistoryItem {
  id: string;
  title: string;
  resourceId: string;
  resourceName: string;
  focusArea: string;
  result: CaseAnalysisResult;
  rawJson: string;
  createdAt: string;
}

@Component({
  selector: 'app-case-analysis',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzSelectModule,
    NzDividerModule,
    NzCollapseModule,
    NzDescriptionsModule,
    NzTagModule,
    NzSpinModule,
    NzIconModule,
    NzEmptyModule,
    NzTabsModule,
    NzTableModule,
    NzPopconfirmModule,
    NzModalModule,
  ],
  templateUrl: './case-analysis.component.html',
  styleUrls: ['./case-analysis.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CaseAnalysisComponent implements OnInit, OnDestroy {
  private readonly chatService = inject(ChatService);
  private readonly messageService = inject(NzMessageService);
  private readonly destroy$ = new Subject<void>();
  private readonly HISTORY_KEY = 'kh-case-analysis-history';

  // 资源列表（左侧）
  resources = signal<ResourceForChat[]>([]);
  resourcesLoading = signal(false);
  resourceFilter = signal('');
  selectedResourceId = signal<string | null>(null);

  selectedResource = computed(() => {
    const id = this.selectedResourceId();
    if (!id) return null;
    return this.resources().find(r => r.id === id) ?? null;
  });

  // 只展示「已生成摘要」的资源，支持按名称 / 格式搜索
  filteredResources = computed(() => {
    const kw = this.resourceFilter().trim().toLowerCase();
    const list = this.resources().filter(r => r.hasSummary === true);
    if (!kw) return list;
    return list.filter(r =>
      (r.name || '').toLowerCase().includes(kw) ||
      (r.sourceFormat ?? '').toLowerCase().includes(kw),
    );
  });

  // 表单输入
  focusArea = signal('');
  result = signal<CaseAnalysisResult | null>(null);
  rawJson = signal('');
  isLoading = signal(false);
  isExporting = signal(false);

  canGenerate = computed(() => {
    const r = this.selectedResource();
    return !!r && r.hasSummary === true && !this.isLoading();
  });

  // 历史记录
  history = signal<CaseAnalysisHistoryItem[]>([]);
  previewItem = signal<CaseAnalysisHistoryItem | null>(null);
  readonly activeTabIndex = signal(0);

  // 前端分页（历史 tab 表格）
  readonly pageIndex = signal(1);
  readonly pageSize = signal(8);
  readonly pagedHistory = computed(() => {
    const start = (this.pageIndex() - 1) * this.pageSize();
    return this.history().slice(start, start + this.pageSize());
  });

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
  }

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
        const parsed: CaseAnalysisHistoryItem[] = JSON.parse(raw);
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

  // ---------- generate ----------
  generate() {
    const resource = this.selectedResource();
    if (!resource || !resource.hasSummary) return;

    this.isLoading.set(true);
    this.result.set(null);
    this.rawJson.set('');

    let fullResponse = '';

    this.chatService.generateCaseAnalysis({
      resourceId: resource.id,
      focusArea: this.focusArea() || undefined
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
          console.error('Error generating case analysis:', err);
          this.isLoading.set(false);
          this.messageService.error('案例分析生成失败，请稍后重试');
        },
        complete: () => {
          this.isLoading.set(false);
          if (fullResponse && !this.result()) {
            this.tryParseResult(fullResponse, true);
          }
          const parsed = this.result();
          const json = this.rawJson();
          if (parsed && json) {
            const item: CaseAnalysisHistoryItem = {
              id: Date.now().toString(36) + Math.random().toString(36).slice(2, 6),
              title: parsed.title || resource.name || '未命名案例分析',
              resourceId: resource.id,
              resourceName: resource.name,
              focusArea: this.focusArea(),
              result: parsed,
              rawJson: json,
              createdAt: new Date().toISOString(),
            };
            this.history.update(list => [item, ...list].slice(0, 50));
            this.saveHistory();

            // 生成成功后清空当前预览、直接跳到历史记录 tab
            this.result.set(null);
            this.rawJson.set('');
            this.activeTabIndex.set(1);
            this.pageIndex.set(1);
            this.messageService.success('案例分析已生成，已保存到历史记录');
          } else if (fullResponse) {
            this.messageService.warning('AI 返回的数据格式不完整，请重新生成');
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

  // ---------- table actions ----------
  previewHistory(item: CaseAnalysisHistoryItem) {
    this.previewItem.set(item);
  }

  backToList() {
    this.previewItem.set(null);
  }

  removeHistory(item: CaseAnalysisHistoryItem) {
    this.history.update(list => list.filter(x => x.id !== item.id));
    this.saveHistory();
    if (this.previewItem()?.id === item.id) {
      this.previewItem.set(null);
    }
    this.messageService.success('已删除');
  }

  async downloadHistory(item: CaseAnalysisHistoryItem) {
    if (!item.rawJson) return;
    this.isExporting.set(true);
    try {
      const blob = await this.chatService.exportCaseAnalysisDocx(item.rawJson);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${item.title || '案例分析'}_${item.createdAt.slice(0, 10)}.docx`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      this.messageService.success('案例分析已下载');
    } catch (err) {
      console.error('Failed to export docx:', err);
      this.messageService.error('导出失败，请重试');
    } finally {
      this.isExporting.set(false);
    }
  }

  async downloadDocx() {
    const json = this.rawJson();
    if (!json) return;

    this.isExporting.set(true);
    try {
      const blob = await this.chatService.exportCaseAnalysisDocx(json);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `案例分析_${new Date().toISOString().slice(0, 10)}.docx`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      this.messageService.success('案例分析已下载');
    } catch (err) {
      console.error('Failed to export docx:', err);
      this.messageService.error('导出失败，请重试');
    } finally {
      this.isExporting.set(false);
    }
  }

  reset() {
    this.selectedResourceId.set(null);
    this.focusArea.set('');
    this.result.set(null);
    this.rawJson.set('');
  }

  selectResource(id: string): void {
    this.selectedResourceId.set(id);
    // 切换资源时清掉上一次结果，避免显示错位
    this.result.set(null);
    this.rawJson.set('');
  }

  getSeverityColor(severity: string): string {
    switch (severity) {
      case '高': return 'red';
      case '中': return 'orange';
      case '低': return 'green';
      default: return 'default';
    }
  }

  formatDate(iso: string): string {
    try {
      const d = new Date(iso);
      return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
    } catch { return iso; }
  }

  trackByHistoryId(_: number, item: CaseAnalysisHistoryItem): string {
    return item.id;
  }
}
