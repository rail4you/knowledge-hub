import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';

import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { MasteryBarComponent } from '../../learning-analytics/components/mastery-bar/mastery-bar.component';
import { MasteryLevel } from '../../learning-analytics/models/learning-analytics.models';
import { LearningAnalyticsService } from '../../proxy/learning-analytics/learning-analytics.service';
import {
  ANALYSIS_TABS,
  AnalysisTab,
  DEFAULT_MASTERY_OK_LINE,
  EMPTY_TEXT,
  MASTERY_LEVEL_LABELS,
  PAPER_CONFIRM_LABELS,
} from './analysis-labels';
import type {
  KnowledgeMasteryItemDto,
  LossAttributionDto,
  PracticeItemDto,
  StudentAnalysisDashboardDto,
  StudentReportDetailDto,
  StudentReportListItemDto,
} from '../../proxy/learning-analytics/dtos/models';

/** 顶部统计卡 */
interface StatCard {
  label: string;
  value: number;
  suffix: string;
  hint: string;
}

/**
 * 学生端「智学分析」。
 *
 * 数据全部来自生成的代理服务（`proxy/learning-analytics`）：
 *   · getStudentDashboard()  → 薄弱点总览 / 知识点掌握度 / 失分归因 / 消息 / 推荐练习 / 学习路径
 *   · getMyReports()         → 老师发布的个人试卷分析（已发布批次）
 *   · getMyReport(id)        → 报告详情（得分 / 薄弱知识点 / 建议 / 学习计划）
 *   · getThreshold(courseId) → 后端配置的掌握度判定线
 *
 * 信任规则（原型 learner-analysis 的硬约束）：
 *   只有老师人工确认过的纸质试卷才能出现在学生端。
 *   后端 `PaperScript.Confirmed` 为 false 时，成绩 / 排名 / 失分归因一律不展示。
 */
@Component({
  selector: 'app-student-analysis',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    MasteryBarComponent,
  ],
  templateUrl: './student-analysis.component.html',
  styleUrls: ['./student-analysis.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAnalysisComponent implements OnInit {
  private readonly service = inject(LearningAnalyticsService);
  private readonly router = inject(Router);

  // ══════════════ 状态 ══════════════

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly dashboard = signal<StudentAnalysisDashboardDto | null>(null);
  readonly activeTab = signal<AnalysisTab>('weakness');

  /** 老师发布的个人试卷分析（列表） */
  readonly reports = signal<StudentReportListItemDto[]>([]);
  readonly reportsLoading = signal(true);
  readonly reportsFailed = signal(false);
  /** 报告详情（已加载的详情集合，用数组代替手写 Record 缓存） */
  readonly reportDetails = signal<StudentReportDetailDto[]>([]);
  readonly detailLoading = signal(false);
  readonly downloadingReportId = signal<string | null>(null);

  /** 后端配置的掌握度达标线（取不到时用兜底常量） */
  readonly okLine = signal<number>(DEFAULT_MASTERY_OK_LINE);

  readonly tabs = ANALYSIS_TABS;
  readonly emptyText = EMPTY_TEXT;
  readonly paperConfirmLabels = PAPER_CONFIRM_LABELS;

  // ══════════════ 计算属性 ══════════════

  readonly overview = computed(() => this.dashboard()?.overview ?? null);
  readonly knowledgePoints = computed<KnowledgeMasteryItemDto[]>(() => this.overview()?.knowledgePoints ?? []);
  readonly notifications = computed(() => this.dashboard()?.notifications ?? []);
  readonly unreadCount = computed(() => this.dashboard()?.unreadCount ?? 0);
  readonly practices = computed<PracticeItemDto[]>(() => this.dashboard()?.practices ?? []);
  readonly path = computed(() => this.dashboard()?.path ?? []);
  readonly reviewProgress = computed(() => this.dashboard()?.reviewProgress ?? null);
  readonly courseName = computed(() => this.overview()?.courseName ?? '');
  readonly courseId = computed(() => this.overview()?.courseId ?? '');

  /** 纸质试卷是否已被老师确认（未确认时不得向学生展示识别结果） */
  readonly paperConfirmed = computed(() => this.overview()?.confirmed === true);

  /** 失分归因同样来自纸质试卷，未确认时一律不展示 */
  readonly losses = computed<LossAttributionDto[]>(() => (this.paperConfirmed() ? this.overview()?.losses ?? [] : []));

  /** 薄弱知识点数（后端已按判定线统计） */
  readonly weakCount = computed(() => this.overview()?.weakCount ?? 0);

  /** 达标知识点数：掌握率 ≥ 生效判定线 */
  readonly reachedCount = computed(
    () => this.knowledgePoints().filter(kp => (kp.mastery ?? 0) >= this.targetOf(kp)).length,
  );

  /** 未读报告数（页签徒标） */
  readonly unreadReportCount = computed(() => this.reports().filter(r => !r.isRead).length);

  /** 顶部四张统计卡 */
  readonly stats = computed<StatCard[]>(() => [
    {
      label: '薄弱知识点',
      value: this.weakCount(),
      suffix: '个',
      hint: '按后端判定线统计',
    },
    {
      label: '已达标知识点',
      value: this.reachedCount(),
      suffix: '个',
      hint: `掌握率 ≥ ${Math.round(this.okLine() * 100)}%`,
    },
    {
      label: '老师已发布报告',
      value: this.reports().length,
      suffix: '份',
      hint: '含得分与补强建议',
    },
    {
      label: '未读学习消息',
      value: this.unreadCount(),
      suffix: '条',
      hint: '成绩反馈与复习提醒',
    },
  ]);

  /** 衔接复习包完成度 */
  readonly reviewPercent = computed(() => {
    const progress = this.reviewProgress();
    if (!progress || !progress.total) return 0;
    return Math.round(((progress.completed ?? 0) / progress.total) * 100);
  });

  /** 最薄弱的知识点（用于「进入专项练习」入口） */
  readonly weakestKnowledgePoint = computed<KnowledgeMasteryItemDto | null>(() => {
    const list = this.knowledgePoints();
    if (!list.length) return null;
    return list.reduce((worst, cur) => ((cur.mastery ?? 0) < (worst.mastery ?? 0) ? cur : worst), list[0]);
  });

  // ══════════════ 生命周期 ══════════════

  ngOnInit(): void {
    this.load();
  }

  /** 整页重试 */
  reload(): void {
    this.loadFailed.set(false);
    this.loading.set(true);
    this.load();
  }

  private load(): void {
    this.service.getStudentDashboard().subscribe({
      next: data => {
        this.dashboard.set(data);
        this.loading.set(false);
        this.loadThreshold(data?.overview?.courseId);
        this.loadReports();
      },
      error: () => {
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  /** 读取后端配置的掌握度判定线 */
  private loadThreshold(courseId?: string): void {
    if (!courseId) {
      this.okLine.set(DEFAULT_MASTERY_OK_LINE);
      return;
    }
    this.service.getThreshold(courseId).subscribe({
      next: threshold => {
        const line = threshold?.okLine;
        this.okLine.set(typeof line === 'number' && line > 0 ? line : DEFAULT_MASTERY_OK_LINE);
      },
      error: () => this.okLine.set(DEFAULT_MASTERY_OK_LINE),
    });
  }

  // ══════════════ 老师发布的报告 ══════════════

  loadReports(): void {
    this.reportsLoading.set(true);
    this.reportsFailed.set(false);
    this.service.getMyReports().subscribe({
      next: list => {
        this.reports.set(list ?? []);
        this.reportsLoading.set(false);
        this.loadReportDetails(list ?? []);
      },
      error: () => {
        this.reports.set([]);
        this.reportDetails.set([]);
        this.reportsFailed.set(true);
        this.reportsLoading.set(false);
      },
    });
  }

  /** 逐份加载报告详情（得分 / 薄弱知识点 / 建议 / 学习计划） */
  private loadReportDetails(list: StudentReportListItemDto[]): void {
    if (!list.length) {
      this.reportDetails.set([]);
      this.detailLoading.set(false);
      return;
    }
    this.detailLoading.set(true);
    const details: StudentReportDetailDto[] = [];
    let pending = list.length;
    const settle = (): void => {
      pending -= 1;
      if (pending > 0) return;
      this.reportDetails.set(details);
      this.detailLoading.set(false);
    };
    list.forEach(item => {
      if (!item.id) {
        settle();
        return;
      }
      this.service.getMyReport(item.id).subscribe({
        next: detail => {
          details.push(detail);
          settle();
        },
        error: () => settle(),
      });
    });
  }

  /** 取某份报告的详情 */
  detailOf(id?: string): StudentReportDetailDto | null {
    if (!id) return null;
    return this.reportDetails().find(d => d.id === id) ?? null;
  }

  /** 消息里的「查看报告」：跳到已发布试卷卡所在的页签 */
  openReportFromMessage(notificationId?: string, relatedId?: string | null): void {
    this.markRead(notificationId, relatedId);
    this.setTab('weakness');
  }

  /** 下载成 Word */
  downloadReport(item: StudentReportListItemDto): void {
    if (!item.id || this.downloadingReportId()) return;

    this.downloadingReportId.set(item.id);
    this.service.downloadMyReport(item.id).subscribe({
      next: blob => {
        this.downloadingReportId.set(null);
        const name = `${item.courseName || '课程'}_${item.title}_学习分析报告.docx`;
        this.saveBlob(blob, name);
      },
      error: () => this.downloadingReportId.set(null),
    });
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName.replace(/[\\/:*?"<>|]/g, '_');
    a.click();
    window.URL.revokeObjectURL(url);
  }

  // ══════════════ 页签与动作 ══════════════

  setTab(tab: AnalysisTab): void {
    this.activeTab.set(tab);
  }

  /** 标记消息已读 */
  markRead(notificationId?: string, relatedId?: string | null): void {
    if (!notificationId) return;
    this.service.markNotificationRead(notificationId).subscribe({ error: () => undefined });
    this.dashboard.update(data =>
      data
        ? {
            ...data,
            unreadCount: Math.max(0, (data.unreadCount ?? 0) - 1),
            notifications: (data.notifications ?? []).map(n =>
              n.id === notificationId ? { ...n, isRead: true } : n,
            ),
          }
        : data,
    );
    if (relatedId) {
      this.reports.update(list => list.map(r => (r.id === relatedId ? { ...r, isRead: true } : r)));
    }
  }

  /** 进入专项练习：带上最薄弱知识点（真实数据） */
  startPractice(item?: PracticeItemDto): void {
    const target = item ?? this.weakestKnowledgePoint();
    const courseId = item?.courseId || this.courseId();
    const kpId = item?.knowledgePointId || this.weakestKnowledgePoint()?.knowledgePointId;
    const kpName = item?.title || this.weakestKnowledgePoint()?.name;

    if (!courseId) {
      this.setTab('practice');
      return;
    }

    this.router.navigate(['/student/practice'], {
      queryParams: {
        courseId,
        knowledgePointId: kpId || undefined,
        kpName: kpName || undefined,
      },
    });
  }

  /** 调整学习路径（页内跳转） */
  adjustPath(): void {
    this.setTab('path');
  }

  /** 去课程中心 */
  goCourses(): void {
    this.router.navigate(['/student/courses']);
  }

  // ══════════════ 展示辅助 ══════════════

  /** 知识点生效的达标线：知识点级 targetLine → 课程级 okLine → 兜底常量 */
  targetOf(kp: KnowledgeMasteryItemDto): number {
    const line = kp?.targetLine;
    if (typeof line === 'number' && line > 0) return line;
    return this.okLine();
  }

  /** 后端等级值 → 掌握度枚举 */
  masteryLevel(level?: number): MasteryLevel | null {
    if (level === undefined || level === null) return null;
    if (level === MasteryLevel.Weak) return MasteryLevel.Weak;
    if (level === MasteryLevel.Fair) return MasteryLevel.Fair;
    if (level === MasteryLevel.Ok) return MasteryLevel.Ok;
    return null;
  }

  /** 等级文案 */
  masteryLevelLabel(level?: number): string {
    const resolved = this.masteryLevel(level);
    if (!resolved) return '';
    return MASTERY_LEVEL_LABELS[resolved];
  }

  /** 按名称匹配知识点掌握项（报告里的薄弱知识点 → 掌握度进度条） */
  masteryOf(name?: string | null): KnowledgeMasteryItemDto | null {
    if (!name) return null;
    return this.knowledgePoints().find(kp => kp.name === name) ?? null;
  }

  /** 分数显示：没有分数时用占位 */
  scoreText(score?: number | null): string {
    return score === null || score === undefined ? '—' : String(score);
  }

  /** 相对时间：刚刚 / N 分钟前 / N 小时前 / 昨天 / N 天前 */
  relativeTime(iso?: string | null): string {
    if (!iso) return '';
    const then = new Date(iso).getTime();
    if (Number.isNaN(then)) return '';
    const diff = Date.now() - then;
    const min = Math.floor(diff / 60000);
    if (min < 1) return '刚刚';
    if (min < 60) return `${min} 分钟前`;
    const hour = Math.floor(min / 60);
    if (hour < 24) return `${hour} 小时前`;
    const day = Math.floor(hour / 24);
    if (day === 1) return '昨天';
    if (day < 30) return `${day} 天前`;
    return new Date(iso).toLocaleDateString('zh-CN');
  }
}
