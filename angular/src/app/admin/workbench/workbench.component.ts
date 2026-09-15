import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { ConfigStateService } from '@abp/ng.core';
import * as echarts from 'echarts/core';
import { LineChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent, GridComponent } from 'echarts/components';
import { TenantListService } from '../../proxy/controllers/tenant-list.service';
import { QuickNavService } from '../../shared/quick-nav/quick-nav.service';
import { WorkbenchService, WorkbenchStats, WorkbenchTrends, WorkbenchUsage } from './workbench.service';

echarts.use([LineChart, TooltipComponent, GridComponent, CanvasRenderer]);

interface TenantOption {
  id: string;
  name: string;
}

interface TrendMetric {
  key: keyof WorkbenchUsage;
  label: string;
  color: string;
}

interface PeriodRow {
  key: string;
  label: string;
  color: string;
  today: number;
  yesterday: number;
  week: number;
}

/**
 * 趋势指标定义：指标列表、折线图与右侧汇总表共用同一份配置。
 * 默认展示「资源检索次数」。
 */
const TREND_METRICS: TrendMetric[] = [
  { key: 'searches', label: '资源检索次数', color: '#1890ff' },
  { key: 'resourceViews', label: '资源浏览', color: '#13c2c2' },
  { key: 'resourceUploads', label: '资源上传', color: '#52c41a' },
  { key: 'aiCalls', label: 'AI 调用', color: '#fa8c16' },
  { key: 'enrollments', label: '选课', color: '#f5222d' },
  { key: 'jobApplications', label: '职位投递', color: '#faad14' },
  { key: 'practicumSubmissions', label: '实训提交', color: '#8c8c8c' },
];

const DEFAULT_METRIC: keyof WorkbenchUsage = 'searches';

/**
 * 系统工作台（管理端首页）。
 *
 * 聚合展示资源、课程、AI、就业、实训、资讯、检索、用户等模块的实时统计，
 * 并用折线图 + 汇总表展示近 7 天使用趋势（折线图指标由列表切换，默认资源检索次数）。
 * - 租户管理员：仅展示本租户数据；
 * - host 全局管理员：头部提供租户列表切换，可查看单个租户或汇总全部租户。
 */
@Component({
  selector: 'app-workbench',
  standalone: true,
  imports: [CommonModule, DecimalPipe, DatePipe, RouterLink, NzIconModule, NzSpinModule, NzEmptyModule],
  templateUrl: './workbench.component.html',
  styleUrls: ['./workbench.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkbenchComponent implements OnInit, OnDestroy {
  private readonly config = inject(ConfigStateService);
  private readonly tenantListService = inject(TenantListService);
  private readonly workbench = inject(WorkbenchService);
  /** 全局快速导航服务（与 QuickNavComponent 共享同一实例）。 */
  readonly quickNav = inject(QuickNavService);

  private readonly trendChartRef = viewChild<ElementRef<HTMLDivElement>>('trendChart');
  private trendChart: echarts.ECharts | null = null;

  readonly stats = signal<WorkbenchStats | null>(null);
  readonly loading = signal(false);
  readonly loadError = signal(false);

  /** 是否 host 全局管理员（无租户上下文）。 */
  readonly isHost = signal(false);
  readonly tenants = signal<TenantOption[]>([]);
  /** null = 全部租户（仅 host 可选）。 */
  readonly selectedTenantId = signal<string | null>(null);

  /** 指标列表（折线图选择器）。 */
  readonly trendMetrics = TREND_METRICS;
  readonly selectedMetricKey = signal<keyof WorkbenchUsage>(DEFAULT_METRIC);

  /** 今天 / 昨天 / 近 7 天 各指标汇总（与图表指标一一对应）。 */
  readonly periodRows = computed<PeriodRow[]>(() => {
    const t = this.stats()?.trends;
    if (!t) {
      return [];
    }
    return TREND_METRICS.map(m => ({
      key: m.key,
      label: m.label,
      color: m.color,
      today: t.today?.[m.key] ?? 0,
      yesterday: t.yesterday?.[m.key] ?? 0,
      week: t.lastDays?.[m.key] ?? 0,
    }));
  });

  readonly scopeName = computed(() => {
    const id = this.selectedTenantId();
    if (id) {
      return this.tenants().find(t => t.id === id)?.name || '';
    }
    if (this.isHost()) {
      return '全部租户';
    }
    // 租户管理员：显示后端返回的当前租户名。
    return this.stats()?.tenantName || '';
  });

  constructor() {
    // 数据、指标或图表容器就绪后渲染/刷新折线图（切换租户/指标时重新渲染）。
    effect(() => {
      const trends = this.stats()?.trends;
      const metricKey = this.selectedMetricKey();
      const el = this.trendChartRef();
      if (!trends || !el) {
        return;
      }
      if (!this.trendChart) {
        this.trendChart = echarts.init(el.nativeElement);
      }
      this.renderTrendChart(trends, metricKey);
    });
  }

  ngOnInit(): void {
    const tenantId = this.config.getDeep('currentUser.tenantId') as string | null | undefined;
    const host = !tenantId;
    this.isHost.set(host);

    if (host) {
      this.tenantListService.getTenants().subscribe({
        next: list => {
          const real = (list || [])
            .filter(t => !!t?.id && !!t?.name)
            .map(t => ({ id: t.id as string, name: t.name as string }));
          this.tenants.set(real);
        },
      });
    }

    this.loadStats();
  }

  ngOnDestroy(): void {
    this.trendChart?.dispose();
    this.trendChart = null;
  }

  @HostListener('window:resize')
  onResize(): void {
    this.trendChart?.resize();
  }

  /** 下拉菜单切换折线图指标。 */
  onMetricChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as keyof WorkbenchUsage;
    this.selectedMetricKey.set(value);
  }

  selectTenant(id: string | null): void {
    if (this.selectedTenantId() === id) {
      return;
    }
    this.selectedTenantId.set(id);
    this.loadStats();
  }

  refresh(): void {
    this.loadStats();
  }

  private loadStats(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.workbench.getStats(this.selectedTenantId()).subscribe({
      next: stats => {
        this.stats.set(stats);
        this.loading.set(false);
      },
      error: () => {
        this.stats.set(null);
        this.loading.set(false);
        this.loadError.set(true);
      },
    });
  }

  /** 百分比展示（0 分母返回 0）。 */
  rate(part: number | undefined, total: number | undefined): number {
    if (!total) {
      return 0;
    }
    return Math.round(((part || 0) / total) * 100);
  }

  /**
   * 全局快捷键：仅保留页面级 `r`（刷新）。
   * 快速导航面板（Ctrl/Cmd+K / Esc / `?`）由根组件 `QuickNavComponent` 全局接管。
   */
  @HostListener('window:keydown', ['$event'])
  onGlobalKeydown(event: KeyboardEvent): void {
    const lower = event.key.toLowerCase();
    if (event.metaKey || event.ctrlKey || event.altKey) {
      return;
    }
    const target = event.target as HTMLElement | null;
    if (target) {
      const tag = target.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target.isContentEditable) {
        return;
      }
    }
    if (lower === 'r') {
      event.preventDefault();
      this.refresh();
    }
  }

  private renderTrendChart(trends: WorkbenchTrends, metricKey: keyof WorkbenchUsage): void {
    const chart = this.trendChart;
    if (!chart) {
      return;
    }
    const daily = trends.daily || [];
    const metric = TREND_METRICS.find(m => m.key === metricKey) ?? TREND_METRICS[0];
    const data = daily.map(d => d[metric.key] ?? 0);

    chart.setOption(
      {
        animation: false,
        tooltip: {
          trigger: 'axis',
          axisPointer: { type: 'line' },
        },
        grid: { left: 6, right: 16, top: 18, bottom: 2, containLabel: true },
        xAxis: {
          type: 'category',
          boundaryGap: false,
          data: daily.map(d => d.label),
          axisLine: { lineStyle: { color: '#e7edf5' } },
          axisTick: { show: false },
          axisLabel: { color: '#5f6f81', fontSize: 12 },
        },
        yAxis: {
          type: 'value',
          minInterval: 1,
          splitLine: { lineStyle: { color: '#eef3f9' } },
          axisLabel: { color: '#5f6f81', fontSize: 12 },
        },
        series: [
          {
            name: metric.label,
            type: 'line',
            smooth: true,
            symbol: 'circle',
            symbolSize: 6,
            showSymbol: true,
            lineStyle: { width: 2.5, color: metric.color },
            itemStyle: { color: metric.color },
            areaStyle: { color: metric.color, opacity: 0.1 },
            data,
          },
        ],
      },
      true,
    );
  }
}
