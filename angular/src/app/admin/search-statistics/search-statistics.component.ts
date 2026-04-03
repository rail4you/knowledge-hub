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
import { NzButtonModule } from 'ng-zorro-antd/button';
import { ConfigStateService, LocalizationModule } from '@abp/ng.core';
import { SearchStatisticsService, SearchDashboardDto } from './search-statistics.service';

@Component({
  selector: 'app-search-statistics',
  standalone: true,
  imports: [
    CommonModule, FormsModule, LocalizationModule, NzCardModule, NzSpinModule, NzStatisticModule,
    NzDatePickerModule, NzSelectModule, NzTableModule, NzTagModule, NzEmptyModule, NzTooltipModule, NzButtonModule
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
    return Math.max(...d.dailyTrends.map(t => t.totalSearchCount), 1);
  });

  maxDocumentSearchCount = computed(() => {
    const d = this.dashboard();
    if (!d || d.dailyTrends.length === 0) return 1;
    return Math.max(...d.dailyTrends.map(t => t.documentSearchCount), 1);
  });

  maxVideoSearchCount = computed(() => {
    const d = this.dashboard();
    if (!d || d.dailyTrends.length === 0) return 1;
    return Math.max(...d.dailyTrends.map(t => t.videoSearchCount), 1);
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

  async exportToExcel() {
    const d = this.dashboard();
    if (!d) return;

    const XLSX = await import('xlsx');
    const wb = XLSX.utils.book_new();

    // 概览数据
    const overview = [
      ['统计项', '数值'],
      ['全部检索次数', d.all.totalSearches],
      ['全部今日检索', d.all.todaySearches],
      ['文档检索次数', d.document.totalSearches],
      ['文档今日检索', d.document.todaySearches],
      ['视频检索次数', d.video.totalSearches],
      ['视频今日检索', d.video.todaySearches]
    ];
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(overview), '概览');

    // 每日趋势
    const trends = [
      ['日期', '全部检索', '文档检索', '视频检索'],
      ...d.dailyTrends.map(t => [t.date, t.totalSearchCount, t.documentSearchCount, t.videoSearchCount])
    ];
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(trends), '每日趋势');

    // 热门搜索
    const popular = [
      ['关键词', '次数', '类型'],
      ...d.popularSearches.map(p => [p.keyword, p.count, p.sourceType === 'video' ? '视频' : p.sourceType === 'document' ? '文档' : '全部'])
    ];
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(popular), '热门搜索');

    // 热门资源
    const resources = [
      ['资源名称', '搜索次数'],
      ...d.topResources.map(r => [r.resourceName, r.searchCount])
    ];
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(resources), '热门资源');

    // 高评分资源
    const rated = [
      ['资源名称', '平均评分', '评价数'],
      ...d.topRatedResources.map(r => [r.resourceName, r.averageRating, r.reviewCount])
    ];
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(rated), '高评分资源');

    const fileName = `搜索统计_${new Date().toISOString().split('T')[0]}.xlsx`;
    XLSX.writeFile(wb, fileName);
    this.message.success('导出成功');
  }
}
