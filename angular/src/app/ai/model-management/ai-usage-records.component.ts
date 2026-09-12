import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfigStateService } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule, NzTableQueryParams } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { Subject, takeUntil } from 'rxjs';
import {
  AiManagementService,
  type AiUsageRecordDto,
  type AiUsageSummaryDto,
} from '../services/ai-management.service';

/**
 * AI 调用记录（独立 Tab）：分页表格 + 汇总，支持按时间区间、任务类型（功能分组）、状态、关键字筛选。
 * 租户管理员只看本租户；host 平台管理员可查看全部租户（多一列“租户”）。
 */
@Component({
  selector: 'app-ai-usage-records',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, FormsModule,
    NzCardModule, NzButtonModule, NzInputModule, NzSelectModule,
    NzTableModule, NzTagModule, NzDatePickerModule, NzGridModule,
  ],
  templateUrl: './ai-usage-records.component.html',
  styleUrls: ['./ai-usage-records.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiUsageRecordsComponent implements OnInit {
  private readonly api = inject(AiManagementService);
  private readonly configState = inject(ConfigStateService);
  private readonly destroy$ = new Subject<void>();

  /** host（平台）查看全部租户，展示“租户”列 */
  readonly isHost = signal(false);

  readonly records = signal<AiUsageRecordDto[]>([]);
  readonly recordsLoading = signal(false);
  readonly total = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(20);
  readonly summary = signal<AiUsageSummaryDto | null>(null);

  /** 日期区间（信号持有稳定引用，避免 getter 每轮返回新数组导致变更检测死循环） */
  readonly dateRange = signal<Date[]>([]);
  readonly filterGroup = signal<string | null>(null);
  readonly filterStatus = signal<number | null>(null);
  readonly filterText = signal('');

  /** 任务类型（功能分组） */
  readonly featureGroups = [
    { value: 'CareerGuidance', label: '职业规划生成' },
    { value: 'LessonPlan', label: '教案生成' },
    { value: 'CaseAnalysis', label: '案例分析' },
    { value: 'ExerciseGenerate', label: '习题生成' },
    { value: 'Chat', label: 'AI 对话' },
    { value: 'Video', label: '视频理解' },
    { value: 'Summary', label: '文档摘要' },
  ];

  ngOnInit(): void {
    const currentUser = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    this.isHost.set(!!currentUser && (currentUser['tenantId'] as string | null | undefined) == null);

    // 默认最近 7 天
    const end = new Date();
    const start = new Date();
    start.setDate(start.getDate() - 7);
    this.dateRange.set([start, end]);
    this.loadRecords();
  }

  private buildFilterInput() {
    const range = this.dateRange();
    return {
      startTime: range?.[0]?.toISOString(),
      endTime: range?.[1]?.toISOString(),
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

  groupLabel(value: string): string {
    return this.featureGroups.find(g => g.value === value)?.label || value;
  }

  statusColor(status: number): string {
    switch (status) {
      case 10: return 'success';
      case 40: return 'error';
      default: return 'processing';
    }
  }
}
