import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
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

/** 闭环步骤卡片 */
interface WorkbenchStep {
  no: string;
  title: string;
  desc: string;
  url: string;
  primary: string;
  secondary: string;
  warn?: string;
  tone: 'blue' | 'cyan' | 'violet' | 'amber' | 'green';
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
export class EmploymentWorkbenchComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly data = signal<EmploymentWorkbenchDto | null>(null);

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
        tone: 'blue',
      },
      {
        no: '②',
        title: '岗位投递',
        desc: '学生投递简历，学校 / 企业查看并推进后续流程。',
        url: '/admin/employment/interviews',
        primary: `${d.applicationTotal} 份投递`,
        secondary: `已录取 ${d.applicationOffered} · 已拒绝 ${d.applicationRejected}`,
        warn: d.applicationPending > 0 ? `${d.applicationPending} 份待处理` : undefined,
        tone: 'cyan',
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
        tone: 'violet',
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
        tone: 'amber',
      },
      {
        no: '⑤',
        title: '就业统计',
        desc: '按去向核实结果汇总就业率与分布，可导出报表。',
        url: '/admin/employment/statistics',
        primary: `就业率 ${d.employmentRate}%`,
        secondary: `已落实就业 ${d.studentEmployed} / 学生 ${d.studentTotal}`,
        tone: 'green',
      },
    ];
  });

  /** 学生转化漏斗（带百分比，用于画条） */
  readonly funnelRows = computed(() => {
    const d = this.data();
    if (!d) {
      return [] as { label: string; value: number; percent: number; hint: string }[];
    }

    const base = Math.max(d.funnel.studentTotal, 1);
    const rows = [
      { label: '本校学生', value: d.funnel.studentTotal, hint: '全部' },
      { label: '有简历', value: d.funnel.withResume, hint: '已完善简历' },
      { label: '已投递', value: d.funnel.withApplication, hint: '投过岗位' },
      { label: '已面试', value: d.funnel.interviewed, hint: '进过面试' },
      { label: '有去向', value: d.funnel.withOutcome, hint: '登记了去向' },
      { label: '去向已核实', value: d.funnel.verified, hint: '学校核实' },
      { label: '已落实就业', value: d.funnel.employed, hint: '计入就业率' },
    ];

    return rows.map(r => ({
      ...r,
      percent: Math.round((r.value / base) * 100),
    }));
  });

  /** 就业去向状态分布（堆叠：已核实 / 待核实） */
  readonly outcomeRows = computed(() => {
    const d = this.data();
    if (!d) {
      return [];
    }
    const max = Math.max(1, ...d.outcomeStatuses.map(x => x.count));
    return d.outcomeStatuses.map(x => ({
      ...x,
      percent: Math.round((x.count / max) * 100),
      verifiedPercent: x.count === 0 ? 0 : Math.round((x.verifiedCount / x.count) * 100),
    }));
  });

  /** 投递状态分布 */
  readonly applicationRows = computed(() => {
    const d = this.data();
    if (!d) {
      return [];
    }
    const max = Math.max(1, ...d.applicationStatuses.map(x => x.count));
    return d.applicationStatuses.map(x => ({
      ...x,
      percent: Math.round((x.count / max) * 100),
    }));
  });

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
