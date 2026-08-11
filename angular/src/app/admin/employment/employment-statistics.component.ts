import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, inject, signal, computed, viewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import * as echarts from 'echarts/core';
import { PieChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent, LegendComponent } from 'echarts/components';
import {
  EmploymentOutcomeDto,
  EmploymentOutcomeStatus,
  EmploymentService,
  StudentApplicationStatDto,
} from '../../employment/employment.service';

echarts.use([PieChart, TooltipComponent, LegendComponent, CanvasRenderer]);

const STATUS_LABELS: Record<number, string> = {
  0: '已投递', 1: '已查看', 2: '面试中', 3: '已录取', 4: '已拒绝', 5: '已撤回', 6: '面试完成',
};
const STATUS_COLORS: Record<number, string> = {
  0: '#1e6ce8', 1: '#6366f1', 2: '#0891b2', 3: '#10b981', 4: '#ef4444', 5: '#94a3b8', 6: '#7c3aed',
};

const OUTCOME_ITEMS: { status: EmploymentOutcomeStatus; label: string; color: string }[] = [
  { status: EmploymentOutcomeStatus.Intention, label: '就业意向', color: '#3b82f6' },
  { status: EmploymentOutcomeStatus.Signed, label: '已签约', color: '#0891b2' },
  { status: EmploymentOutcomeStatus.Employed, label: '已就业', color: '#10b981' },
  { status: EmploymentOutcomeStatus.FurtherStudy, label: '升学', color: '#8b5cf6' },
  { status: EmploymentOutcomeStatus.Entrepreneurship, label: '创业', color: '#f59e0b' },
  { status: EmploymentOutcomeStatus.Unemployed, label: '待就业', color: '#94a3b8' },
];

interface StudentGroup {
  studentId: string;
  studentName: string;
  items: StudentApplicationStatDto[];
}

@Component({
  selector: 'app-employment-statistics',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzCardModule, NzTagModule, NzTableModule, NzIconModule, NzEmptyModule, NzSpinModule, NzRadioModule, NzDividerModule],
  templateUrl: './employment-statistics.component.html',
  styleUrls: ['./employment-statistics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentStatisticsComponent implements OnInit, OnDestroy {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly items = signal<StudentApplicationStatDto[]>([]);
  readonly days = signal<number | null>(null); // 默认显示全部（切换后可看近7/30天）

  // ===== 就业去向 =====
  readonly outcomeLoading = signal(false);
  readonly outcomes = signal<EmploymentOutcomeDto[]>([]);

  // ===== 图表 =====
  private readonly outcomeChartRef = viewChild<ElementRef<HTMLDivElement>>('outcomeChart');
  private readonly applicationChartRef = viewChild<ElementRef<HTMLDivElement>>('applicationChart');
  private outcomeChart: echarts.ECharts | null = null;
  private applicationChart: echarts.ECharts | null = null;

  readonly outcomeTotal = computed(() => this.outcomes().length);
  readonly outcomeEmployed = computed(() => this.outcomes().filter(x => x.status === EmploymentOutcomeStatus.Employed).length);
  readonly outcomeSigned = computed(() => this.outcomes().filter(x => x.status === EmploymentOutcomeStatus.Signed).length);
  readonly outcomePrimary = computed(() => this.outcomes().filter(x => x.isPrimary).length);
  readonly outcomeRate = computed(() => {
    const total = this.outcomeTotal();
    if (total === 0) return 0;
    return Math.round(((this.outcomeEmployed() + this.outcomeSigned()) / total) * 100);
  });

  /** 按学生分组 */
  readonly groups = computed<StudentGroup[]>(() => {
    const map = new Map<string, StudentGroup>();
    for (const item of this.items()) {
      const key = item.studentId || 'unknown';
      if (!map.has(key)) {
        map.set(key, { studentId: key, studentName: item.studentName || '未知', items: [] });
      }
      map.get(key)!.items.push(item);
    }
    return Array.from(map.values()).sort((a, b) => b.items.length - a.items.length);
  });

  readonly totalStudents = computed(() => this.groups().length);
  readonly totalApplications = computed(() => this.items().length);

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.outcomeChart?.dispose();
    this.applicationChart?.dispose();
    this.outcomeChart = null;
    this.applicationChart = null;
  }

  load(): void {
    this.loading.set(true);
    this.employmentService.getApplicationStats({
      days: this.days() ?? undefined,
    }).subscribe({
      next: data => {
        this.items.set(data || []);
        this.loading.set(false);
        this.initCharts();
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载失败');
      },
    });
    this.loadOutcomes();
  }

  loadOutcomes(): void {
    this.outcomeLoading.set(true);
    this.employmentService.getOutcomeList({ skipCount: 0, maxResultCount: 1000 }).subscribe({
      next: result => {
        this.outcomes.set(result.items || []);
        this.outcomeLoading.set(false);
        this.initCharts();
      },
      error: () => {
        this.outcomeLoading.set(false);
      },
    });
  }

  setDays(d: number | null): void {
    this.days.set(d);
    this.load();
  }

  statusLabel(s: number): string { return STATUS_LABELS[s] ?? `未知(${s})`; }
  statusColor(s: number): string { return STATUS_COLORS[s] ?? '#94a3b8'; }

  outcomeStatusLabel(s?: EmploymentOutcomeStatus): string {
    const m: Record<number, string> = {
      [EmploymentOutcomeStatus.Intention]: '就业意向',
      [EmploymentOutcomeStatus.Signed]: '已签约',
      [EmploymentOutcomeStatus.Employed]: '已就业',
      [EmploymentOutcomeStatus.FurtherStudy]: '升学',
      [EmploymentOutcomeStatus.Entrepreneurship]: '创业',
      [EmploymentOutcomeStatus.Unemployed]: '待就业',
    };
    return m[s ?? -1] ?? '未知';
  }

  outcomeStatusColor(s?: EmploymentOutcomeStatus): string {
    const m: Record<number, string> = {
      [EmploymentOutcomeStatus.Intention]: 'blue',
      [EmploymentOutcomeStatus.Signed]: 'cyan',
      [EmploymentOutcomeStatus.Employed]: 'green',
      [EmploymentOutcomeStatus.FurtherStudy]: 'purple',
      [EmploymentOutcomeStatus.Entrepreneurship]: 'gold',
      [EmploymentOutcomeStatus.Unemployed]: 'default',
    };
    return m[s ?? -1] ?? 'default';
  }

  export(): void {
    this.employmentService.exportStatistics({
      days: this.days() ?? undefined,
    }).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `employment-stats-${Date.now()}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.message.error('导出失败'),
    });
  }

  // ===== 图表 =====
  private initCharts(): void {
    setTimeout(() => {
      this.initOutcomeChart();
      this.initApplicationChart();
    }, 100);
  }

  private initOutcomeChart(): void {
    const container = this.outcomeChartRef()?.nativeElement;
    if (!container) return;
    this.outcomeChart?.dispose();
    this.outcomeChart = echarts.init(container);

    const counts = OUTCOME_ITEMS.map(item => ({
      value: this.outcomes().filter(x => x.status === item.status).length,
      name: item.label,
      itemStyle: { color: item.color },
    }));

    this.outcomeChart.setOption({
      tooltip: { trigger: 'item', formatter: '{b}：{c} 条 ({d}%)', backgroundColor: '#fff', borderColor: '#e8ecf1', textStyle: { color: '#1e293b', fontSize: 13 } },
      legend: { bottom: 0, icon: 'circle', itemWidth: 10, itemHeight: 10, textStyle: { color: '#64748b', fontSize: 12 } },
      series: [{
        name: '就业去向状态',
        type: 'pie',
        radius: ['45%', '70%'],
        center: ['50%', '45%'],
        avoidLabelOverlap: true,
        itemStyle: { borderRadius: 6, borderColor: '#fff', borderWidth: 2 },
        label: { show: false },
        emphasis: { label: { show: true, fontSize: 15, fontWeight: 'bold' } },
        data: counts,
      }],
    });
  }

  private initApplicationChart(): void {
    const container = this.applicationChartRef()?.nativeElement;
    if (!container) return;
    this.applicationChart?.dispose();
    this.applicationChart = echarts.init(container);

    const statuses = this.items().reduce<Record<number, number>>((acc, item) => {
      acc[item.status] = (acc[item.status] || 0) + 1;
      return acc;
    }, {});
    const data = Object.keys(statuses).map(key => {
      const s = Number(key);
      return { value: statuses[s], name: STATUS_LABELS[s] ?? `未知(${s})`, itemStyle: { color: STATUS_COLORS[s] ?? '#94a3b8' } };
    });

    this.applicationChart.setOption({
      tooltip: { trigger: 'item', formatter: '{b}：{c} 次 ({d}%)', backgroundColor: '#fff', borderColor: '#e8ecf1', textStyle: { color: '#1e293b', fontSize: 13 } },
      legend: { bottom: 0, icon: 'circle', itemWidth: 10, itemHeight: 10, textStyle: { color: '#64748b', fontSize: 12 } },
      series: [{
        name: '投递状态',
        type: 'pie',
        radius: ['45%', '70%'],
        center: ['50%', '45%'],
        avoidLabelOverlap: true,
        itemStyle: { borderRadius: 6, borderColor: '#fff', borderWidth: 2 },
        label: { show: false },
        emphasis: { label: { show: true, fontSize: 15, fontWeight: 'bold' } },
        data,
      }],
    });
  }

}
