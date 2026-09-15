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
import { Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { ConfigStateService } from '@abp/ng.core';
import * as echarts from 'echarts/core';
import { LineChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent, GridComponent } from 'echarts/components';
import { TenantListService } from '../../proxy/controllers/tenant-list.service';
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
 * 模块导航定义：每个模块卡片可点击 / 可键盘跳转到对应后台页面。
 * - `prefixKey`：`g` 后接的字母（GitHub 风格的前缀组合键）；
 * - `numberKey`：直接数字键 1-8 跳转到第 N 个模块。
 */
interface ModuleNav {
  prefixKey: string;
  numberKey: number;
  label: string;
  icon: string;
  route: string;
  description: string;
}

const MODULE_NAV: ModuleNav[] = [
  { prefixKey: 'r', numberKey: 1, label: '资源管理', icon: 'folder-open', route: '/resources', description: '资源总量、审核状态与使用情况' },
  { prefixKey: 'c', numberKey: 2, label: '专业和课程', icon: 'read', route: '/learning/course-list', description: '课程、章节、习题与专业建设' },
  { prefixKey: 'a', numberKey: 3, label: 'AI 管理', icon: 'robot', route: '/ai/tasks', description: 'AI 调用、Token 与费用估算' },
  { prefixKey: 'e', numberKey: 4, label: '就业管理', icon: 'idcard', route: '/admin/employment/statistics', description: '职位、申请、面试与就业成果' },
  { prefixKey: 'p', numberKey: 5, label: '实训', icon: 'experiment', route: '/admin/practicum/projects', description: '实训项目、任务、报名与提交' },
  { prefixKey: 'n', numberKey: 6, label: '资讯管理', icon: 'file-text', route: '/admin/news', description: '资讯发布与审核情况' },
  { prefixKey: 's', numberKey: 7, label: '检索统计', icon: 'search', route: '/admin/search-statistics', description: '检索量与活跃用户' },
  { prefixKey: 'u', numberKey: 8, label: '用户概览', icon: 'user', route: '/identity/users', description: '参与教学的师生规模' },
];

/** 全局快捷键清单（用于 ? 帮助面板展示）。 */
const GLOBAL_SHORTCUTS: { keys: string[]; label: string; description: string }[] = [
  { keys: ['g', 'r'], label: '前往资源管理', description: '按 g 后接 r，跳转到资源管理页' },
  { keys: ['g', 'c'], label: '前往专业和课程', description: '按 g 后接 c，跳转到课程列表' },
  { keys: ['g', 'a'], label: '前往 AI 管理', description: '按 g 后接 a，跳转到 AI 任务中心' },
  { keys: ['g', 'e'], label: '前往就业管理', description: '按 g 后接 e，跳转到就业统计' },
  { keys: ['g', 'p'], label: '前往实训', description: '按 g 后接 p，跳转到实训管理' },
  { keys: ['g', 'n'], label: '前往资讯管理', description: '按 g 后接 n，跳转到资讯管理' },
  { keys: ['g', 's'], label: '前往检索统计', description: '按 g 后接 s，跳转到检索统计' },
  { keys: ['g', 'u'], label: '前往用户概览', description: '按 g 后接 u，跳转到用户管理' },
  { keys: ['1', '8'], label: '数字快捷跳转', description: '按 1-8 直接跳转到对应模块卡片' },
  { keys: ['r'], label: '刷新工作台', description: '重新加载统计数据' },
  { keys: ['?'], label: '快捷键帮助', description: '打开 / 关闭本帮助面板' },
  { keys: ['Esc'], label: '关闭 / 取消', description: '关闭帮助或取消 g 前缀等待' },
];

/** `g` 前缀等待超时（ms）：超过即视为放弃。 */
const G_PREFIX_TIMEOUT_MS = 1200;

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
  private readonly router = inject(Router);

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

  // ── 快捷导航 / 快捷键 ──────────────────────────────
  readonly moduleNavs = MODULE_NAV;
  readonly globalShortcuts = GLOBAL_SHORTCUTS;
  /** 快捷键帮助面板开关（`?` 切换）。 */
  readonly helpOpen = signal(false);
  /** 按下 `g` 后等待第二键的状态（带页面顶部提示条）。 */
  readonly gPrefixPending = signal(false);
  /** 当前被快捷键锁定的目标模块 key（用于卡片瞬时高亮反馈）。 */
  readonly focusedNavKey = signal<string | null>(null);

  private gPrefixTimer: ReturnType<typeof setTimeout> | null = null;
  private focusFlashTimer: ReturnType<typeof setTimeout> | null = null;

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
    if (this.gPrefixTimer) clearTimeout(this.gPrefixTimer);
    if (this.focusFlashTimer) clearTimeout(this.focusFlashTimer);
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
   * 全局快捷键监听：
   * - `g` + 字母：按 MODULE_NAV.prefixKey 跳转到对应模块；
   * - `1`-`8`：按数字直接跳转到对应模块卡片；
   * - `r`：刷新工作台数据；
   * - `?`（或 Shift+`/`）：打开 / 关闭帮助面板；
   * - `Esc`：关闭帮助或取消 g 前缀等待。
   *
   * 输入控件（INPUT / TEXTAREA / SELECT / contenteditable）获焦时自动忽略，
   * 避免劫持表单输入。下拉框的指标切换不会被触发。
   */
  @HostListener('window:keydown', ['$event'])
  onGlobalKeydown(event: KeyboardEvent): void {
    // 修饰键：让浏览器原生快捷键（Ctrl+R 刷新等）正常工作。
    if (event.metaKey || event.ctrlKey || event.altKey) {
      return;
    }

    const target = event.target as HTMLElement | null;
    if (target) {
      const tag = target.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') {
        return;
      }
      if (target.isContentEditable) {
        return;
      }
    }

    const key = event.key;
    const lower = key.toLowerCase();

    // 帮助面板打开时：只响应 Esc / `?` 关闭，其它按键透传，避免与面板交互冲突。
    if (this.helpOpen()) {
      if (key === 'Escape' || key === '?' || (event.shiftKey && lower === '/')) {
        this.helpOpen.set(false);
        this.cancelGPrefix();
        event.preventDefault();
      }
      return;
    }

    // Esc 优先：关闭帮助 / 取消 g 前缀等待。
    if (key === 'Escape') {
      if (this.gPrefixPending()) {
        this.cancelGPrefix();
        event.preventDefault();
        return;
      }
      return;
    }

    // `?` 切换帮助（兼容 Shift+/）。
    if (key === '?' || (event.shiftKey && lower === '/')) {
      this.helpOpen.update(v => !v);
      this.cancelGPrefix();
      event.preventDefault();
      return;
    }

    // `g` 进入前缀等待（只在未等待时进入；二次按 g 不重置计时）。
    if (lower === 'g' && !this.gPrefixPending()) {
      this.enterGPrefix();
      event.preventDefault();
      return;
    }

    // g 前缀等待中：消费下一个字符。
    if (this.gPrefixPending()) {
      const nav = MODULE_NAV.find(m => m.prefixKey === lower);
      if (nav) {
        this.goToModule(nav);
        this.cancelGPrefix();
        event.preventDefault();
        return;
      }
      // 其他字符：取消前缀等待，不阻止默认行为。
      this.cancelGPrefix();
      return;
    }

    // 数字直接跳转（1-8）。长度 = 1 排除方向键等。
    if (key.length === 1 && key >= '1' && key <= '8') {
      const idx = parseInt(key, 10) - 1;
      const nav = MODULE_NAV[idx];
      if (nav) {
        this.goToModule(nav);
        event.preventDefault();
        return;
      }
    }

    // `r` 刷新（前面已经被 `g r` 组合消费过；这里只在非前缀态下触发）。
    if (lower === 'r') {
      this.refresh();
      event.preventDefault();
      return;
    }
  }

  /** 跳转到指定模块（点击卡片 / 快捷键共用）。 */
  goToModule(nav: ModuleNav): void {
    if (!nav?.route) {
      return;
    }
    // 短暂高亮目标卡片，给键盘操作一个可见的"确认反馈"。
    this.focusedNavKey.set(nav.prefixKey);
    if (this.focusFlashTimer) {
      clearTimeout(this.focusFlashTimer);
    }
    this.focusFlashTimer = setTimeout(() => this.focusedNavKey.set(null), 700);
    this.router.navigateByUrl(nav.route);
  }

  /** 关闭帮助面板（点击遮罩或右上角关闭按钮）。 */
  closeHelp(): void {
    this.helpOpen.set(false);
  }

  /** 关闭帮助 / 取消 g 前缀时阻止冒泡到 keydown 监听。 */
  swallowEvent(event: Event): void {
    event.stopPropagation();
  }

  private enterGPrefix(): void {
    this.gPrefixPending.set(true);
    if (this.gPrefixTimer) {
      clearTimeout(this.gPrefixTimer);
    }
    this.gPrefixTimer = setTimeout(() => this.cancelGPrefix(), G_PREFIX_TIMEOUT_MS);
  }

  private cancelGPrefix(): void {
    this.gPrefixPending.set(false);
    if (this.gPrefixTimer) {
      clearTimeout(this.gPrefixTimer);
      this.gPrefixTimer = null;
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
