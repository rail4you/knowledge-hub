import { AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import * as echarts from 'echarts/core';
import { PieChart, LineChart } from 'echarts/charts';
import { CanvasRenderer } from 'echarts/renderers';
import { TooltipComponent, LegendComponent, GridComponent } from 'echarts/components';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import {
  PracticumEnrollmentDto,
  PracticumGuidanceRecordDto,
  PracticumTimelineItemDto,
} from '../../proxy/practicums/dtos/models';
import { PracticumEnrollmentStatus } from '../../proxy/practicums/enums/practicum-enrollment-status.enum';

echarts.use([PieChart, LineChart, CanvasRenderer, TooltipComponent, LegendComponent, GridComponent]);

  @Component({
  selector: 'app-student-my-practicums',
  standalone: true,
  imports: [
    CommonModule, DatePipe, RouterModule,
    NzButtonModule, NzIconModule, NzSpinModule, NzProgressModule, NzTagModule, NzEmptyModule, NzModalModule,
  ],
  templateUrl: './student-my-practicums.component.html',
  styleUrls: ['./student-my-practicums.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyPracticumsComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly practicumService = inject(PracticumService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly items = signal<PracticumEnrollmentDto[]>([]);
  readonly loading = signal(true);

  readonly guidanceVisible = signal(false);
  readonly guidanceLoading = signal(false);
  readonly guidanceItems = signal<PracticumGuidanceRecordDto[]>([]);
  guidanceTitle = '';

  readonly timelineVisible = signal(false);
  readonly timelineLoading = signal(false);
  readonly timelineItems = signal<PracticumTimelineItemDto[]>([]);
  timelineTitle = '';

  // ─── 统计汇总 ───────────────────────────
  readonly summary = signal({
    total: 0, enrolled: 0, inProgress: 0, submitted: 0, reviewed: 0, completed: 0, cancelled: 0, avgProgress: 0,
  });

  @ViewChild('pieChartEl') pieChartEl?: ElementRef<HTMLDivElement>;
  @ViewChild('lineChartEl') lineChartEl?: ElementRef<HTMLDivElement>;
  private pieChart: echarts.ECharts | null = null;
  private lineChart: echarts.ECharts | null = null;

  ngOnInit(): void {
    this.reload();
  }

  ngAfterViewInit(): void {
    this.renderCharts();
  }

  ngOnDestroy(): void {
    this.pieChart?.dispose();
    this.lineChart?.dispose();
    this.pieChart = null;
    this.lineChart = null;
  }

  reload(): void {
    this.loading.set(true);
    this.practicumService.getMyEnrollments().subscribe({
      next: items => {
        this.items.set(items || []);
        this.loading.set(false);
        this.computeSummary();
        setTimeout(() => this.renderCharts(), 0);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载我的实训失败');
      },
    });
  }

  private computeSummary(): void {
    const items = this.items();
    const s = { total: items.length, enrolled: 0, inProgress: 0, submitted: 0, reviewed: 0, completed: 0, cancelled: 0, avgProgress: 0 };
    let progressSum = 0;
    for (const it of items) {
      progressSum += it.progress || 0;
      switch (it.status) {
        case PracticumEnrollmentStatus.Enrolled: s.enrolled++; break;
        case PracticumEnrollmentStatus.InProgress: s.inProgress++; break;
        case PracticumEnrollmentStatus.Submitted: s.submitted++; break;
        case PracticumEnrollmentStatus.Reviewed: s.reviewed++; break;
        case PracticumEnrollmentStatus.Completed: s.completed++; break;
        case PracticumEnrollmentStatus.Cancelled: s.cancelled++; break;
      }
    }
    s.avgProgress = s.total ? Math.round(progressSum / s.total) : 0;
    this.summary.set(s);
  }

  private renderCharts(): void {
    const items = this.items();
    if (!this.pieChartEl?.nativeElement || !this.lineChartEl?.nativeElement || !items.length) return;

    // 饼图：状态分布
    const statusItems: [PracticumEnrollmentStatus, string][] = [
      [PracticumEnrollmentStatus.InProgress, '进行中'],
      [PracticumEnrollmentStatus.Submitted, '待评阅'],
      [PracticumEnrollmentStatus.Reviewed, '已评阅'],
      [PracticumEnrollmentStatus.Completed, '已完成'],
      [PracticumEnrollmentStatus.Cancelled, '已取消'],
      [PracticumEnrollmentStatus.Enrolled, '已参与'],
    ];
    const pieData = statusItems
      .map(([st, label]) => ({ name: label, value: items.filter(i => i.status === st).length }))
      .filter(d => d.value > 0);

    if (!this.pieChart) this.pieChart = echarts.init(this.pieChartEl.nativeElement);
    this.pieChart.setOption({
      tooltip: { trigger: 'item' },
      legend: { bottom: 0, icon: 'circle', textStyle: { fontSize: 12 } },
      series: [{
        type: 'pie',
        radius: ['42%', '68%'],
        center: ['50%', '44%'],
        avoidLabelOverlap: true,
        label: { show: true, formatter: '{b}\n{c}' },
        itemStyle: { borderRadius: 6, borderColor: '#fff', borderWidth: 2 },
        data: pieData,
      }],
    });

    // 折线图：各实训项目进度（按报名时间排序）
    const sorted = [...items].sort((a, b) => new Date(a.enrolledAt).getTime() - new Date(b.enrolledAt).getTime());
    const labels = sorted.map(i => i.projectTitle || '未命名');
    const values = sorted.map(i => i.progress || 0);

    if (!this.lineChart) this.lineChart = echarts.init(this.lineChartEl.nativeElement);
    this.lineChart.setOption({
      tooltip: { trigger: 'axis' },
      grid: { left: 40, right: 20, top: 30, bottom: 50 },
      xAxis: { type: 'category', data: labels, axisLabel: { interval: 0, rotate: 30, fontSize: 11 } },
      yAxis: { type: 'value', max: 100, axisLabel: { formatter: '{value}%' } },
      series: [{
        type: 'line',
        smooth: true,
        symbolSize: 7,
        data: values,
        lineStyle: { width: 3, color: '#2563eb' },
        itemStyle: { color: '#2563eb' },
        areaStyle: { color: 'rgba(37,99,235,0.12)' },
        label: { show: true, formatter: '{c}%' },
      }],
    });
  }

  openDetail(item: PracticumEnrollmentDto): void {
    this.router.navigate(['/student/practicums', item.projectId]);
  }

  openGuidance(item: PracticumEnrollmentDto): void {
    this.guidanceTitle = `${item.projectTitle || '实训项目'} · 指导记录`;
    this.guidanceVisible.set(true);
    this.guidanceLoading.set(true);
    this.guidanceItems.set([]);
    this.practicumService.getGuidanceList(item.id).subscribe({
      next: list => {
        this.guidanceItems.set(list || []);
        this.guidanceLoading.set(false);
      },
      error: () => {
        this.guidanceLoading.set(false);
        this.message.error('加载指导记录失败');
      },
    });
  }

  openTimeline(item: PracticumEnrollmentDto): void {
    this.timelineTitle = `${item.projectTitle || '实训项目'} · 过程追溯`;
    this.timelineVisible.set(true);
    this.timelineLoading.set(true);
    this.timelineItems.set([]);
    this.practicumService.getTimeline(item.id).subscribe({
      next: list => {
        this.timelineItems.set(list || []);
        this.timelineLoading.set(false);
      },
      error: () => {
        this.timelineLoading.set(false);
        this.message.error('加载过程记录失败');
      },
    });
  }

  getStatusLabel(status?: PracticumEnrollmentStatus): string {
    const labels: Record<number, string> = {
      [PracticumEnrollmentStatus.Enrolled]: '已参与',
      [PracticumEnrollmentStatus.InProgress]: '进行中',
      [PracticumEnrollmentStatus.Submitted]: '待评阅',
      [PracticumEnrollmentStatus.Reviewed]: '已评阅',
      [PracticumEnrollmentStatus.Completed]: '已完成',
      [PracticumEnrollmentStatus.Cancelled]: '已取消',
    };
    return labels[status ?? -1] || '未知';
  }

  getStatusColor(status?: PracticumEnrollmentStatus): string {
    switch (status) {
      case PracticumEnrollmentStatus.InProgress: return 'processing';
      case PracticumEnrollmentStatus.Submitted: return 'warning';
      case PracticumEnrollmentStatus.Reviewed: return 'cyan';
      case PracticumEnrollmentStatus.Completed: return 'success';
      case PracticumEnrollmentStatus.Cancelled: return 'default';
      default: return 'blue';
    }
  }

  hasScore(item: PracticumEnrollmentDto): boolean {
    return item.finalScore != null;
  }

  hasMetadata(item: PracticumTimelineItemDto, key: string): boolean {
    return item.metadata?.[key] != null;
  }

  timelineTypeLabel(type?: string): string {
    switch (type) {
      case 'Enrollment': return '参与实训';
      case 'Submission': return '提交成果';
      case 'Guidance': return '教师指导';
      case 'Assessment': return '教师评分';
      default: return type || '';
    }
  }

  timelineTypeColor(type?: string): string {
    switch (type) {
      case 'Submission': return 'blue';
      case 'Guidance': return 'green';
      case 'Assessment': return 'gold';
      default: return 'default';
    }
  }
}
