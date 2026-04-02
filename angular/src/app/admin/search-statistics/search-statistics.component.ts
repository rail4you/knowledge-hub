import { Component, inject, signal, OnInit, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { ConfigStateService, LocalizationModule } from '@abp/ng.core';
import { SearchStatisticsService, SearchDashboardDto } from './search-statistics.service';

@Component({
  selector: 'app-search-statistics',
  standalone: true,
  imports: [
    CommonModule, FormsModule, LocalizationModule, NzCardModule, NzSpinModule, NzStatisticModule,
    NzDatePickerModule, NzSelectModule, NzTableModule, NzTagModule, NzEmptyModule, NzTooltipModule
  ],
  templateUrl: './search-statistics.component.html',
  styleUrls: ['./search-statistics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SearchStatisticsComponent implements OnInit {
  private readonly statsService = inject(SearchStatisticsService);
  private readonly configService = inject(ConfigStateService);
  private readonly message = inject(NzMessageService);

  loading = signal(false);
  dashboard = signal<SearchDashboardDto | null>(null);
  dateRange: Date[] | null = null;
  selectedTenantId: string | null = null;
  isHost = false;
  tenants = signal<{ id: string; name: string }[]>([]);

  maxTrendSearchCount = computed(() => {
    const d = this.dashboard();
    if (!d || d.dailyTrends.length === 0) return 1;
    return Math.max(...d.dailyTrends.map(t => t.searchCount), 1);
  });

  ngOnInit() {
    this.isHost = !this.configService.getDeep('currentUser.tenantId');
    if (this.isHost) {
      this.loadTenants();
    }
    this.loadData();
  }

  private loadTenants() {
    const tenants = this.configService.getDeep('currentUser.tenants') as any[];
    if (tenants && Array.isArray(tenants)) {
      this.tenants.set(tenants.map((t: any) => ({ id: t.id, name: t.name })));
    }
  }

  loadData() {
    this.loading.set(true);
    const input: any = {};

    if (this.dateRange && this.dateRange[0]) {
      input.startDate = this.dateRange[0].toISOString();
    }
    if (this.dateRange && this.dateRange[1]) {
      input.endDate = this.dateRange[1].toISOString();
    }
    if (this.selectedTenantId) {
      input.tenantId = this.selectedTenantId;
    }

    this.statsService.getDashboard(input).subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.message.error('加载统计数据失败');
        this.loading.set(false);
      }
    });
  }

  onDateRangeChange() { this.loadData(); }
  onTenantChange() { this.loadData(); }

  getTrendHeight(count: number): number {
    const max = this.maxTrendSearchCount();
    if (max === 0) return 4;
    return Math.max(4, (count / max) * 120);
  }
}
