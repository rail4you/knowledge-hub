import {
  ChangeDetectionStrategy,
  Component,
  EffectRef,
  ElementRef,
  OnDestroy,
  OnInit,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import * as echarts from 'echarts/core';
import { BarChart, PieChart } from 'echarts/charts';
import { GridComponent, LegendComponent, TooltipComponent } from 'echarts/components';
import { LabelLayout } from 'echarts/features';
import { CanvasRenderer } from 'echarts/renderers';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import {
  EmploymentService,
  EmploymentWorkbenchDto,
  EmploymentWorkbenchTaskDto,
} from '../../employment/employment.service';

// 按需注册 echarts 模块（环形图 + 横向条形图）
echarts.use([PieChart, BarChart, TooltipComponent, LegendComponent, GridComponent, CanvasRenderer, LabelLayout]);

/** 闭环步骤卡片 */
interface WorkbenchStep {
  no: string;
  title: string;
  desc: string;
  url: string;
  primary: string;
  secondary: string;
  warn?: string;
}

@Component({
  selector: 'app-employment-workbench',
  standalone: true,
  imports: [
    CommonModule,
    NzButtonModule,
    NzIconModule,
    NzProgressModule,
    NzSpinModule,
    NzTooltipModule,
  ],
  templateUrl: './employment-workbench.component.html',
  styleUrls: ['./employment-workbench.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentWorkbenchComponent implements OnInit, OnDestroy {
  private readonly employmentService = inject(EmploymentService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly data = signal<EmploymentWorkbenchDto | null>(null);

  /** 就业去向分布（环形图）与投递状态分布（横向条形图） */
  private readonly outcomeChartRef = viewChild<ElementRef<HTMLElement>>('outcomeChart');
  private readonly applicationChartRef = viewChild<ElementRef<HTMLElement>>('applicationChart');
  private chartEffect: EffectRef | null = null;
  private outcomeChart: echarts.ECharts | null = null;
  private applicationChart: echarts.ECharts | null = null;
  private resizeObserver: ResizeObserver | null = null;

  constructor() {
    // effect 在变更检测之后运行，此刻 @if 里的图表容器与 viewChild 都已就绪；
    // 用 setTimeout 在 zoneless 下可能早于 CD，拿不到容器。
    this.chartEffect = effect(() => {
      const d = this.data();
      const pie = this.outcomeChartRef()?.nativeElement;
      const bar = this.applicationChartRef()?.nativeElement;
      if (!d) {
        return;
      }
      if (pie) {
        this.renderOutcomeChart(d, pie);
      }
      if (bar) {
        this.renderApplicationChart(d, bar);
      }
    });
  }

  readonly steps = computed<WorkbenchStep[]>(() => {
    const d = this.data();
    if (!d) {
      return [];
    }

    return [
      {
        no: '①',
        title: '岗位发布',
        desc: '发布合作企业岗位，学校审核后进入岗位大厅供学生投递。',
        url: '/admin/employment/jobs',
        primary: `${d.jobPublished} 个已发布`,
        secondary: `共 ${d.jobTotal} 个岗位 · 已关闭 ${d.jobClosed}`,
        warn: d.jobPendingReview > 0 ? `${d.jobPendingReview} 个待审核` : undefined,
      },
      {
        no: '②',
        title: '岗位投递',
        desc: '学生投递简历，学校 / 企业查看并推进后续流程。',
        url: '/admin/employment/interviews',
        primary: `${d.applicationTotal} 份投递`,
        secondary: `已录取 ${d.applicationOffered} · 已拒绝 ${d.applicationRejected}`,
        warn: d.applicationPending > 0 ? `${d.applicationPending} 份待处理` : undefined,
      },
      {
        no: '③',
        title: '面试',
        desc: '安排面试、记录面试结果，通过后转为录取并沉淀就业去向。',
        url: '/admin/employment/interviews',
        primary: `${d.interviewTotal} 场面试`,
        secondary: `通过 ${d.interviewPassed} · 未通过 ${d.interviewFailed}`,
        warn:
          d.interviewPendingResult > 0
            ? `${d.interviewPendingResult} 场待录结果`
            : d.interviewUpcoming > 0
              ? `${d.interviewUpcoming} 场待面试`
              : undefined,
      },
      {
        no: '④',
        title: '就业去向',
        desc: '登记学生的就业去向并核实，只有核实通过的去向才计入正式统计。',
        url: '/admin/employment/outcomes',
        primary: `${d.outcomeTotal} 条去向`,
        secondary: `已核实 ${d.outcomeVerified} · 待核实 ${d.outcomeUnverified}`,
        warn:
          d.outcomePrimaryUnverified > 0
            ? `${d.outcomePrimaryUnverified} 名学生的主要去向待核实`
            : d.outcomeUnverified > 0
              ? `${d.outcomeUnverified} 条待核实`
              : undefined,
      },
      {
        no: '⑤',
        title: '就业统计',
        desc: '按去向核实结果汇总就业率与分布，可导出报表。',
        url: '/admin/employment/statistics',
        primary: `就业率 ${d.employmentRate}%`,
        secondary: `已落实就业 ${d.studentEmployed} / 学生 ${d.studentTotal}`,
      },
    ];
  });

  /** 去向状态的配色（环形图用） */
  private static readonly OUTCOME_COLORS: Record<number, string> = {
    1: '#0f766e', // 已签约
    2: '#34a89b', // 已就业
    3: '#7c3aed', // 升学
    4: '#d97706', // 创业
    0: '#94a3b8', // 就业意向
    5: '#cbd5e1', // 待就业
  };

  /**
   * 投递状态的配色（条形图用）：统一绿色系，用颜色深浅表示推进程度。
   * 已投递最浅 → 已录取最深；已拒绝 / 已撤回属于结束态，用低饱和灰绿区分。
   */
  private static readonly APPLICATION_COLORS: Record<number, string> = {
    0: '#d7ebe6', // 已投递（最浅）
    1: '#b3dcd4', // 已查看
    2: '#7fc4b9', // 面试中
    6: '#4ba79b', // 面试完成
    3: '#0f766e', // 已录取（最深）
    4: '#b9c9c3', // 已拒绝（低饱和灰绿）
    5: '#e3eae6', // 已撤回（近乎灰）
  };

  /** 空闭环提醒：哪些环节还没数据 */
  readonly emptyHints = computed(() => {
    const d = this.data();
    if (!d) {
      return [] as string[];
    }
    const hints: string[] = [];
    if (d.jobPublished === 0) {
      hints.push('还没有已发布的岗位，先发布岗位，学生才有可投递的岗位。');
    }
    if (d.applicationTotal === 0) {
      hints.push('还没有学生投递，可提醒学生完善简历并到岗位大厅投递。');
    }
    if (d.interviewTotal === 0 && d.applicationTotal > 0) {
      hints.push('已有投递但还没安排面试，可在「岗位投递和面试管理」里推进。');
    }
    if (d.outcomeTotal === 0) {
      hints.push('还没有登记就业去向，面试通过后请及时登记并核实去向。');
    }
    if (d.outcomeUnverified > 0) {
      hints.push(`有 ${d.outcomeUnverified} 条就业去向待核实，核实后才会计入就业率。`);
    }
    return hints;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.employmentService.getWorkbench().subscribe({
      next: data => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('工作台数据加载失败');
      },
    });
  }

  ngOnDestroy(): void {
    this.chartEffect?.destroy();
    this.chartEffect = null;
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
    this.outcomeChart?.dispose();
    this.applicationChart?.dispose();
    this.outcomeChart = null;
    this.applicationChart = null;
  }

  /** 就业去向分布：环形图（切片=去向状态，环心显示总数 / 已核实数） */
  private renderOutcomeChart(d: EmploymentWorkbenchDto, container: HTMLElement): void {
    this.outcomeChart?.dispose();
    this.outcomeChart = echarts.init(container);

    const items = d.outcomeStatuses
      .filter(x => x.count > 0)
      .map(x => ({
        value: x.count,
        name: x.label,
        itemStyle: {
          color: EmploymentWorkbenchComponent.OUTCOME_COLORS[x.key] ?? '#94a3b8',
        },
      }));

    this.outcomeChart.setOption({
      // 关闭入场动画：后台标签页的 requestAnimationFrame 会被暂停，
      // 动画停在第一帧（环 / 柱宽度为 0），表现为「图表一片空白」。
      animation: false,
      tooltip: {
        trigger: 'item',
        formatter: '{b}：{c} 条（{d}%）',
        backgroundColor: '#fff',
        borderColor: '#e8ecf1',
        textStyle: { color: '#1e293b', fontSize: 12 },
      },
      legend: {
        bottom: 0,
        icon: 'circle',
        itemWidth: 8,
        itemHeight: 8,
        itemGap: 12,
        textStyle: { color: '#697e88', fontSize: 11.5 },
      },
      series: [
        {
          name: '就业去向',
          type: 'pie',
          radius: ['52%', '74%'],
          center: ['50%', '44%'],
          itemStyle: { borderRadius: 4, borderColor: '#fff', borderWidth: 2 },
          label: {
            show: true,
            position: 'inside',
            formatter: (p: any) => ((p.percent ?? 0) >= 8 ? `${Math.round(p.percent)}%` : ''),
            fontSize: 11,
            fontWeight: 600,
            color: '#fff',
          },
          labelLine: { show: false },
          data: items.length === 0 ? [] : items,
        },
      ],
      graphic: [
        {
          type: 'text',
          left: 'center',
          top: '38%',
          style: {
            text: `${d.outcomeTotal}`,
            fill: '#1e293b',
            fontSize: 20,
            fontWeight: 700,
            textAlign: 'center',
          },
        },
        {
          type: 'text',
          left: 'center',
          top: '50%',
          style: {
            text: `已核实 ${d.outcomeVerified}`,
            fill: '#697e88',
            fontSize: 11,
            textAlign: 'center',
          },
        },
      ],
    });

    this.observeResize(container);
  }

  /** 投递状态分布：横向条形图（值为 0 的状态不画） */
  private renderApplicationChart(d: EmploymentWorkbenchDto, container: HTMLElement): void {
    this.applicationChart?.dispose();
    this.applicationChart = echarts.init(container);

    const rows = d.applicationStatuses.filter(x => x.count > 0).reverse();
    const labels = rows.map(x => x.label);
    // 显式算轴上限：所有状态计数相同时（常见于都为 1），echarts 自动缩放会得到
    // [1,1] 退化区间，柱子宽度算成 0（看不见）。留 15% 余量让最长条不至于顶满。
    const axisMax = Math.max(1, Math.ceil(Math.max(...rows.map(x => x.count), 1) * 1.15));
    const values = rows.map(x => ({
      value: x.count,
      itemStyle: { color: EmploymentWorkbenchComponent.APPLICATION_COLORS[x.key] ?? '#94a3b8' },
    }));

    this.applicationChart.setOption({
      animation: false,
      tooltip: {
        trigger: 'item',
        formatter: '{b}：{c} 份',
        backgroundColor: '#fff',
        borderColor: '#e8ecf1',
        textStyle: { color: '#1e293b', fontSize: 12 },
      },
      grid: { left: 56, right: 34, top: 8, bottom: 24 },
      xAxis: {
        type: 'value',
        min: 0,
        max: axisMax,
        minInterval: 1,
        splitLine: { lineStyle: { color: '#f0f3ee' } },
        axisLabel: { color: '#9aa7a0', fontSize: 11 },
      },
      yAxis: {
        type: 'category',
        data: labels,
        axisTick: { show: false },
        axisLine: { show: false },
        axisLabel: { color: '#5d6b63', fontSize: 11.5 },
      },
      series: [
        {
          name: '投递',
          type: 'bar',
          barWidth: 12,
          itemStyle: { borderRadius: [0, 6, 6, 0] },
          label: {
            show: true,
            position: 'right',
            color: '#5d6b63',
            fontSize: 11,
            formatter: '{c}',
          },
          data: values,
        },
      ],
    });

    this.observeResize(container);
  }

  private observeResize(container: HTMLElement): void {
    this.resizeObserver?.disconnect();
    if (typeof ResizeObserver === 'undefined') {
      return;
    }
    this.resizeObserver = new ResizeObserver(() => {
      this.outcomeChart?.resize();
      this.applicationChart?.resize();
    });
    this.resizeObserver.observe(container);
  }

  open(url: string): void {
    void this.router.navigate([url]);
  }

  formatTime(value?: string): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (isNaN(date.getTime())) {
      return value;
    }
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${date.getMonth() + 1}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  trackTask(_: number, item: EmploymentWorkbenchTaskDto): string {
    return item.id;
  }
}
