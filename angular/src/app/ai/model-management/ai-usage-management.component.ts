import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule, NzTableQueryParams } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { Subject, takeUntil } from 'rxjs';
import {
  AiManagementService,
  type AiManagementStatusDto,
  type AiUsageRecordDto,
  type AiUsageSummaryDto,
} from '../services/ai-management.service';

interface QuotaRow {
  role: string;
  values: Record<string, string>;
}

@Component({
  selector: 'app-ai-usage-management',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, FormsModule,
    NzCardModule, NzButtonModule, NzInputModule, NzSelectModule,
    NzTableModule, NzTagModule, NzIconModule, NzSpinModule,
    NzEmptyModule, NzDatePickerModule, NzDescriptionsModule, NzGridModule,
  ],
  templateUrl: './ai-usage-management.component.html',
  styleUrls: ['./ai-usage-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiUsageManagementComponent implements OnInit {
  private readonly api = inject(AiManagementService);
  private readonly message = inject(NzMessageService);
  private readonly destroy$ = new Subject<void>();

  // ============= Key 与模型 =============
  readonly status = signal<AiManagementStatusDto | null>(null);
  readonly statusLoading = signal(false);
  readonly newApiKey = signal('');
  readonly savingKey = signal(false);
  readonly testing = signal(false);

  // ============= 用量记录 =============
  readonly records = signal<AiUsageRecordDto[]>([]);
  readonly recordsLoading = signal(false);
  readonly total = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(20);
  readonly summary = signal<AiUsageSummaryDto | null>(null);

  readonly filterStart = signal<Date | null>(null);
  readonly filterEnd = signal<Date | null>(null);
  readonly filterGroup = signal<string | null>(null);
  readonly filterStatus = signal<number | null>(null);
  readonly filterText = signal('');

  readonly featureGroups = [
    { value: 'CareerGuidance', label: '职业规划生成' },
    { value: 'LessonPlan', label: '教案生成' },
    { value: 'CaseAnalysis', label: '案例分析' },
    { value: 'ExerciseGenerate', label: '习题生成' },
    { value: 'Chat', label: 'AI 对话' },
    { value: 'Video', label: '视频理解' },
    { value: 'Summary', label: '文档摘要' },
  ];

  // ============= 配额 =============
  readonly quotaGroups = ['CareerGuidance', 'Chat', 'LessonPlan', 'CaseAnalysis', 'ExerciseGenerate'];
  readonly quotaRoles = ['Student', 'Teacher', 'SchoolAdmin', 'admin'];
  readonly quotaRows = signal<QuotaRow[]>([]);
  readonly quotasLoading = signal(false);
  readonly savingQuotas = signal(false);

  ngOnInit(): void {
    // 默认最近 7 天
    const end = new Date();
    const start = new Date();
    start.setDate(start.getDate() - 7);
    this.filterStart.set(start);
    this.filterEnd.set(end);
    this.loadStatus();
    this.loadQuotas();
    this.loadRecords();
  }

  groupLabel(value: string): string {
    return this.featureGroups.find(g => g.value === value)?.label || value;
  }

  /** 日期区间双向绑定桥（range-picker 需要数组形态） */
  get dateRange(): Date[] {
    const s = this.filterStart();
    const e = this.filterEnd();
    return s && e ? [s, e] : [];
  }

  onDateRangeChange(dates: Date[]): void {
    this.filterStart.set(dates?.[0] || null);
    this.filterEnd.set(dates?.[1] || null);
    this.onFilterChange();
  }

  // ---------- Key ----------
  loadStatus(): void {
    this.statusLoading.set(true);
    this.api.getStatus()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: s => {
          this.status.set(s);
          this.statusLoading.set(false);
        },
        error: () => {
          this.statusLoading.set(false);
          this.message.error('加载 AI 服务状态失败（需要 AI 管理权限）');
        },
      });
  }

  saveApiKey(): void {
    const key = this.newApiKey().trim();
    if (!key) {
      this.message.warning('请先输入新的 API Key');
      return;
    }
    this.savingKey.set(true);
    this.api.updateApiKey(key)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.savingKey.set(false);
          this.newApiKey.set('');
          this.message.success('API Key 已更新，新调用即时生效，无需重启');
          this.loadStatus();
        },
        error: err => {
          this.savingKey.set(false);
          this.message.error('保存失败：' + (err?.error?.error?.message || '未知错误'));
        },
      });
  }

  testConnection(): void {
    this.testing.set(true);
    this.api.testConnection()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: ok => {
          this.testing.set(false);
          if (ok) this.message.success('连接正常，API Key 有效');
          else this.message.error('连接失败，请检查 Key 是否正确或是否欠费');
        },
        error: () => {
          this.testing.set(false);
          this.message.error('连接失败，请检查 Key 是否正确或是否欠费');
        },
      });
  }

  // ---------- 用量 ----------
  private buildFilterInput() {
    return {
      startTime: this.filterStart()?.toISOString(),
      endTime: this.filterEnd()?.toISOString(),
      featureGroup: this.filterGroup() || undefined,
      status: this.filterStatus() ?? undefined,
      filter: this.filterText().trim() || undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    };
  }

  loadRecords(): void {
    this.recordsLoading.set(true);
    const input = this.buildFilterInput();
    this.api.getUsageRecords(input)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.records.set(res.items || []);
          this.total.set(res.totalCount || 0);
          this.recordsLoading.set(false);
        },
        error: () => this.recordsLoading.set(false),
      });
    this.api.getUsageSummary(input)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: s => this.summary.set(s),
        error: () => {},
      });
  }

  onTableQuery(params: NzTableQueryParams): void {
    const { pageIndex, pageSize } = params;
    if (pageIndex !== this.pageIndex() || pageSize !== this.pageSize()) {
      this.pageIndex.set(pageIndex);
      this.pageSize.set(pageSize);
      this.loadRecords();
    }
  }

  onFilterChange(): void {
    this.pageIndex.set(1);
    this.loadRecords();
  }

  clearFilters(): void {
    this.filterGroup.set(null);
    this.filterStatus.set(null);
    this.filterText.set('');
    this.onFilterChange();
  }

  statusColor(status: number): string {
    switch (status) {
      case 10: return 'success';
      case 40: return 'error';
      default: return 'processing';
    }
  }

  // ---------- 配额 ----------
  loadQuotas(): void {
    this.quotasLoading.set(true);
    this.api.getQuotas()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          const quotas = res.quotas || {};
          const roles = Array.from(new Set([...this.quotaRoles, ...Object.keys(quotas)]));
          this.quotaRows.set(roles.map(role => ({
            role,
            values: Object.fromEntries(
              this.quotaGroups.map(g => [g, quotas[role]?.[g] == null ? '' : String(quotas[role][g])])),
          })));
          this.quotasLoading.set(false);
        },
        error: () => this.quotasLoading.set(false),
      });
  }

  saveQuotas(): void {
    const quotas: Record<string, Record<string, number | null>> = {};
    for (const row of this.quotaRows()) {
      quotas[row.role] = {};
      for (const g of this.quotaGroups) {
        const raw = (row.values[g] || '').trim();
        if (!raw) {
          quotas[row.role][g] = null;
          continue;
        }
        const n = Number(raw);
        if (!Number.isInteger(n) || n < 0) {
          this.message.warning(`配额格式错误：${row.role} / ${this.groupLabel(g)}（填空=不限，非负整数）`);
          return;
        }
        quotas[row.role][g] = n;
      }
    }
    this.savingQuotas.set(true);
    this.api.updateQuotas({ quotas })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.savingQuotas.set(false);
          this.message.success('配额已更新，即时生效');
        },
        error: err => {
          this.savingQuotas.set(false);
          this.message.error('保存失败：' + (err?.error?.error?.message || '未知错误'));
        },
      });
  }
}
