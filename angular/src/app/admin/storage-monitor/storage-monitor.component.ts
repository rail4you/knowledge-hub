import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPageHeaderModule } from 'ng-zorro-antd/page-header';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import {
  StorageMonitorService,
  StorageOverviewDto,
  TenantStorageUsageDto,
} from './storage-monitor.service';

const GB = 1024 * 1024 * 1024;

@Component({
  selector: 'app-storage-monitor',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    NzAlertModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzPageHeaderModule,
    NzPaginationModule,
    NzProgressModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
  ],
  templateUrl: './storage-monitor.component.html',
  styleUrls: ['./storage-monitor.component.scss'],
})
export class StorageMonitorComponent implements OnInit {
  private readonly monitor = inject(StorageMonitorService);
  private readonly message = inject(NzMessageService);

  readonly overview = signal<StorageOverviewDto | null>(null);
  readonly rows = signal<TenantStorageUsageDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly savingDefault = signal(false);

  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  keyword = '';
  statusFilter = 'all';
  sorting = 'usedBytes desc';

  readonly statusOptions = [
    { value: 'all', label: '全部状态' },
    { value: 'exceeded', label: '已超配额' },
    { value: 'warn', label: '接近配额' },
    { value: 'normal', label: '正常' },
    { value: 'unused', label: '未使用' },
  ];

  readonly sortOptions = [
    { value: 'usedBytes desc', label: '已用容量（高→低）' },
    { value: 'fileCount desc', label: '文件数（高→低）' },
    { value: 'recent30dBytes desc', label: '近 30 天增量（高→低）' },
    { value: 'lastUploadTime desc', label: '最近上传时间' },
    { value: 'tenantName asc', label: '租户名称' },
  ];

  // 默认配额（GB 展示，回传时换算成字节）
  defaultQuotaGb = 50;
  warnPercent = 80;

  // 单租户配额编辑弹窗
  readonly quotaModalOpen = signal(false);
  readonly quotaSaving = signal(false);
  quotaTenantName = '';
  quotaTenantId = '';
  quotaGb: number | null = null;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.monitor.getOverview().subscribe({
      next: res => {
        this.overview.set(res);
        this.defaultQuotaGb = res.defaultQuotaBytes > 0 ? +(res.defaultQuotaBytes / GB).toFixed(2) : 0;
        this.warnPercent = res.warnPercent || 80;
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.message.error(extractError(err, '读取存储总览失败'));
      },
    });

    this.loadRows();
  }

  loadRows(): void {
    this.monitor
      .getTenantList({
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
        sorting: this.sorting,
        keyword: this.keyword?.trim() || undefined,
        statusFilter: this.statusFilter,
      })
      .subscribe({
        next: res => {
          this.rows.set(res.items || []);
          this.totalCount.set(res.totalCount || 0);
        },
        error: err => this.message.error(extractError(err, '读取租户用量失败')),
      });
  }

  onFilterChange(): void {
    this.pageIndex.set(1);
    this.loadRows();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.loadRows();
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
    this.loadRows();
  }

  saveDefault(): void {
    if (this.defaultQuotaGb == null || this.defaultQuotaGb < 0) {
      this.message.warning('请输入合法的默认配额');
      return;
    }
    this.savingDefault.set(true);
    this.monitor
      .updateDefault({ quotaBytes: Math.round(this.defaultQuotaGb * GB), warnPercent: this.warnPercent })
      .subscribe({
        next: res => {
          this.savingDefault.set(false);
          this.overview.set(res);
          this.message.success('默认配额已保存');
          this.loadRows();
        },
        error: err => {
          this.savingDefault.set(false);
          this.message.error(extractError(err, '保存失败'));
        },
      });
  }

  openQuotaModal(row: TenantStorageUsageDto): void {
    this.quotaTenantId = row.tenantId;
    this.quotaTenantName = row.tenantName;
    this.quotaGb = row.quotaBytes > 0 ? +(row.quotaBytes / GB).toFixed(2) : 0;
    this.quotaModalOpen.set(true);
  }

  saveQuota(): void {
    if (this.quotaGb == null || this.quotaGb < 0) {
      this.message.warning('请输入合法配额，填 0 表示不限');
      return;
    }
    this.quotaSaving.set(true);
    this.monitor.updateQuota({ tenantId: this.quotaTenantId, quotaBytes: Math.round(this.quotaGb * GB) }).subscribe({
      next: () => {
        this.quotaSaving.set(false);
        this.quotaModalOpen.set(false);
        this.message.success('租户配额已保存');
        this.loadRows();
      },
      error: err => {
        this.quotaSaving.set(false);
        this.message.error(extractError(err, '保存失败'));
      },
    });
  }

  // ── 展示辅助 ────────────────────────────────────────────────
  formatBytes(bytes?: number | null): string {
    const value = Number(bytes ?? 0);
    if (!value) return '0 B';
    const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
    const i = Math.min(Math.floor(Math.log(value) / Math.log(1024)), units.length - 1);
    const n = value / Math.pow(1024, i);
    return `${n.toFixed(i === 0 ? 0 : 2)} ${units[i]}`;
  }

  progressPercent(row: TenantStorageUsageDto): number {
    if (row.quotaBytes <= 0) return 0;
    return Math.min(100, Math.round(row.usagePercent * 10) / 10);
  }

  progressColor(row: TenantStorageUsageDto): string {
    switch (row.status) {
      case 'exceeded':
        return '#f5222d';
      case 'warn':
        return '#faad14';
      case 'unused':
        return '#d9d9d9';
      default:
        return '#52c41a';
    }
  }

  statusText(row: TenantStorageUsageDto): string {
    switch (row.status) {
      case 'exceeded':
        return '已超配额';
      case 'warn':
        return '接近配额';
      case 'unlimited':
        return '不限额';
      case 'unused':
        return '未使用';
      default:
        return '正常';
    }
  }

  statusColor(row: TenantStorageUsageDto): string {
    switch (row.status) {
      case 'exceeded':
        return 'red';
      case 'warn':
        return 'orange';
      case 'unused':
        return 'default';
      default:
        return 'green';
    }
  }

  quotaText(row: TenantStorageUsageDto): string {
    return row.quotaBytes > 0 ? this.formatBytes(row.quotaBytes) : '不限';
  }
}

function extractError(err: unknown, fallback: string): string {
  const e = err as { error?: { error?: { message?: string } }; message?: string };
  return e?.error?.error?.message || e?.message || fallback;
}
