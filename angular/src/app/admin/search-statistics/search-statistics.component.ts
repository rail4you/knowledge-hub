import { Component, inject, signal, OnInit, computed, ChangeDetectionStrategy, ElementRef, viewChild, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzSpinModule } from 'ng-zorro-antd/spin';
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
import * as echarts from 'echarts/core';
import { PieChart, BarChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent, LegendComponent, GridComponent } from 'echarts/components';

echarts.use([PieChart, BarChart, TooltipComponent, LegendComponent, GridComponent, CanvasRenderer]);

@Component({
  selector: 'app-search-statistics',
  standalone: true,
  imports: [
    CommonModule, FormsModule, LocalizationModule, NzSpinModule,
    NzDatePickerModule, NzSelectModule, NzTableModule, NzTagModule, NzEmptyModule, NzTooltipModule, NzButtonModule
  ],
  templateUrl: './search-statistics.component.html',
  styleUrls: ['./search-statistics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SearchStatisticsComponent implements OnInit, OnDestroy {
  private readonly statsService = inject(SearchStatisticsService);
  private readonly configService = inject(ConfigStateService);
  private readonly message = inject(NzMessageService);

  private readonly typeChartRef = viewChild<ElementRef<HTMLDivElement>>('typeChart');
  private typeChart: echarts.ECharts | null = null;

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

  // 类型分布数据（供 ECharts 饼图使用）
  typeDistribution = computed(() => {
    const d = this.dashboard();
    if (!d) return [];
    return [
      { name: '文档检索', value: d.document.totalSearches },
      { name: '视频检索', value: d.video.totalSearches },
    ];
  });

  typeDistributionTotal = computed(() => {
    const dist = this.typeDistribution();
    return dist.reduce((s, i) => s + i.value, 0);
  });

  ngOnInit() {
    this.isHost = !this.configService.getDeep('currentUser.tenantId');
    if (this.isHost) {
      this.loadTenants();
    }
    this.loadData();
  }

  ngOnDestroy() {
    this.typeChart?.dispose();
  }

  private initTypeChart() {
    const el = this.typeChartRef();
    if (!el) return;
    this.typeChart?.dispose();
    this.typeChart = echarts.init(el.nativeElement);
    this.updateTypeChart();
  }

  private updateTypeChart() {
    const chart = this.typeChart;
    const dist = this.typeDistribution();
    if (!chart || dist.length === 0) return;
    chart.setOption({
      tooltip: {
        trigger: 'item',
        formatter: '{b}: {c} ({d}%)'
      },
      legend: {
        show: false,
      },
      series: [{
        type: 'pie',
        radius: ['46%', '72%'],
        avoidLabelOverlap: true,
        center: ['50%', '46%'],
        itemStyle: {
          borderRadius: 6,
          borderWidth: 0,
        },
        label: {
          show: true,
          formatter: '{b}',
          fontSize: 13,
          fontWeight: 600,
          color: '#1c2733',
        },
        emphasis: {
          label: { show: true, fontSize: 14, fontWeight: 700 },
          itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.15)' },
        },
        data: [
          { name: '文档检索', value: dist[0].value, itemStyle: { color: '#52c41a' } },
          { name: '视频检索', value: dist[1].value, itemStyle: { color: '#722ed1' } },
        ],
      }],
    });
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
        // 等 DOM 渲染完成再初始化 ECharts（#typeChart 在 @if 内，首次不存在）
        setTimeout(() => this.initTypeChart());
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
    return Math.max(4, (count / max) * 160);
  }

  async exportToExcel() {
    const d = this.dashboard();
    if (!d) return;

    const XLSW = await import('xlsx');
    const wb = XLSW.utils.book_new();

    const overview = [
      ['统计项', '数值'],
      ['全部检索次数', d.all.totalSearches],
      ['全部今日检索', d.all.todaySearches],
      ['全部活跃用户', d.all.activeUsers],
      ['文档检索次数', d.document.totalSearches],
      ['文档今日检索', d.document.todaySearches],
      ['文档活跃用户', d.document.activeUsers],
      ['视频检索次数', d.video.totalSearches],
      ['视频今日检索', d.video.todaySearches],
      ['视频活跃用户', d.video.activeUsers]
    ];
    XLSW.utils.book_append_sheet(wb, XLSW.utils.aoa_to_sheet(overview), '概览');

    const trends = [
      ['日期', '全部检索', '文档检索', '视频检索'],
      ...d.dailyTrends.map(t => [t.date, t.totalSearchCount, t.documentSearchCount, t.videoSearchCount])
    ];
    XLSW.utils.book_append_sheet(wb, XLSW.utils.aoa_to_sheet(trends), '每日趋势');

    const popular = [
      ['排名', '关键词', '次数', '类型'],
      ...d.popularSearches.map((p, i) => [i + 1, p.keyword, p.count, p.sourceType === 'video' ? '视频' : p.sourceType === 'document' ? '文档' : '全部'])
    ];
    XLSW.utils.book_append_sheet(wb, XLSW.utils.aoa_to_sheet(popular), '热门搜索');

    const resources = [
      ['排名', '资源名称', '阅读量'],
      ...d.topResources.map((r, i) => [i + 1, r.resourceName, r.viewCount])
    ];
    XLSW.utils.book_append_sheet(wb, XLSW.utils.aoa_to_sheet(resources), '热门资源');

    const rated = [
      ['排名', '资源名称', '平均评分', '评价数'],
      ...d.topRatedResources.map((r, i) => [i + 1, r.resourceName, r.averageRating, r.reviewCount])
    ];
    XLSW.utils.book_append_sheet(wb, XLSW.utils.aoa_to_sheet(rated), '高评分资源');

    const fileName = `搜索统计_${new Date().toISOString().split('T')[0]}.xlsx`;
    XLSW.writeFile(wb, fileName);
    this.message.success('导出成功');
  }
}