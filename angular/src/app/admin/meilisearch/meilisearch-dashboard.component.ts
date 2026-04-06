import { Component, inject, signal, computed, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MeiliSearchAdminService, MeiliDashboardDto, MeiliEmbedderDto, MeiliDocumentGroupDto, PageIndexListItemDto } from './meilisearch-admin.service';

@Component({
  selector: 'app-meilisearch-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzTagModule,
    NzTableModule,
    NzSpinModule,
    NzEmptyModule,
    NzGridModule,
    NzDescriptionsModule,
    NzIconModule,
    NzBadgeModule,
    NzTooltipModule,
    NzRadioModule,
  ],
  templateUrl: './meilisearch-dashboard.component.html',
  styleUrls: ['./meilisearch-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MeiliSearchDashboardComponent implements OnInit {
  private readonly adminService = inject(MeiliSearchAdminService);
  private readonly message = inject(NzMessageService);

  loading = signal(false);
  dashboard = signal<MeiliDashboardDto | null>(null);
  expandedIndexUid = signal<string | null>(null);
  documentGroups = signal<Record<string, MeiliDocumentGroupDto[]>>({});
  loadingDocuments = signal<Record<string, boolean>>({});
  expandedGroupName = signal<string | null>(null);
  timeFilter = signal<string>('all');
  pageIndexList = signal<PageIndexListItemDto[]>([]);
  loadingPageIndex = signal(false);
  expandedPageIndexId = signal<string | null>(null);

  totalNodeCount = computed(() => {
    return this.pageIndexList().reduce((sum, p) => sum + p.nodeCount, 0);
  });

  filteredDocumentGroups = computed(() => {
    const indexUid = this.expandedIndexUid();
    if (!indexUid) return [];
    const groups = this.documentGroups()[indexUid] ?? [];
    const filter = this.timeFilter();
    if (filter === 'all') return groups;

    const now = new Date();
    const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate());

    switch (filter) {
      case 'today':
        return groups.filter(g => {
          if (!g.uploadDate) return false;
          return new Date(g.uploadDate) >= todayStart;
        });
      case 'yesterday': {
        const yesterdayStart = new Date(todayStart.getTime() - 86400000);
        return groups.filter(g => {
          if (!g.uploadDate) return false;
          const d = new Date(g.uploadDate);
          return d >= yesterdayStart && d < todayStart;
        });
      }
      case 'week':
        return groups.filter(g => {
          if (!g.uploadDate) return false;
          return new Date(g.uploadDate) >= new Date(todayStart.getTime() - 7 * 86400000);
        });
      case 'month':
        return groups.filter(g => {
          if (!g.uploadDate) return false;
          return new Date(g.uploadDate) >= new Date(todayStart.getTime() - 30 * 86400000);
        });
      default:
        return groups;
    }
  });

  ngOnInit() {
    this.loadDashboard();
    this.loadPageIndexList();
  }

  loadDashboard() {
    this.loading.set(true);
    this.adminService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载搜索引擎数据失败');
      },
    });
  }

  refresh() {
    this.loadDashboard();
    this.loadPageIndexList();
  }

  toggleIndexDetail(uid: string) {
    const current = this.expandedIndexUid();
    if (current === uid) {
      this.expandedIndexUid.set(null);
      return;
    }
    this.expandedIndexUid.set(uid);
    this.loadDocuments(uid);
  }

  loadDocuments(indexUid: string) {
    this.loadingDocuments.update(m => ({ ...m, [indexUid]: true }));
    this.adminService.getIndexDocuments(indexUid).subscribe({
      next: (groups) => {
        this.documentGroups.update(m => ({ ...m, [indexUid]: groups }));
        this.loadingDocuments.update(m => ({ ...m, [indexUid]: false }));
      },
      error: () => {
        this.loadingDocuments.update(m => ({ ...m, [indexUid]: false }));
        this.message.error('加载文档数据失败');
      },
    });
  }

  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  getStatusColor(status: string): string {
    switch (status) {
      case 'enqueued': return 'default';
      case 'processing': return 'processing';
      case 'succeeded': return 'success';
      case 'failed': return 'error';
      case 'canceled': return 'warning';
      default: return 'default';
    }
  }

  getStatusText(status: string): string {
    switch (status) {
      case 'enqueued': return '排队中';
      case 'processing': return '处理中';
      case 'succeeded': return '成功';
      case 'failed': return '失败';
      case 'canceled': return '已取消';
      default: return status;
    }
  }

  embedderEntries(embedders: Record<string, MeiliEmbedderDto>): { name: string; embedder: MeiliEmbedderDto }[] {
    return Object.entries(embedders).map(([name, embedder]) => ({ name, embedder }));
  }

  toggleGroupExpand(resourceName: string) {
    const current = this.expandedGroupName();
    this.expandedGroupName.set(current === resourceName ? null : resourceName);
  }

  truncateText(text: string | null | undefined, maxLength = 80): string {
    if (!text) return '-';
    return text.length > maxLength ? text.substring(0, maxLength) + '...' : text;
  }

  loadPageIndexList() {
    this.loadingPageIndex.set(true);
    this.adminService.getPageIndexList().subscribe({
      next: (data) => {
        this.pageIndexList.set(data);
        this.loadingPageIndex.set(false);
      },
      error: () => {
        this.loadingPageIndex.set(false);
        this.message.error('加载 PageIndex 数据失败');
      },
    });
  }

  togglePageIndexDetail(id: string) {
    const current = this.expandedPageIndexId();
    this.expandedPageIndexId.set(current === id ? null : id);
  }
}
