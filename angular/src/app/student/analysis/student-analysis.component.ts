import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';

import { SectionBannerComponent } from '../shared/section-banner/section-banner.component';
import { MasteryBarComponent } from '../../learning-analytics/components/mastery-bar/mastery-bar.component';
import { LearningAnalyticsService } from '../../learning-analytics/services/learning-analytics.service';
import {
  AnalysisNotification,
  MasteryLevel,
  PracticeItem,
  StudentAnalysisDashboard,
} from '../../learning-analytics/models/learning-analytics.models';

type AnalysisTab = 'weakness' | 'path' | 'messages' | 'practice';

/** 学生端「智学分析」：薄弱点 / 学习路径 / 消息 / 专项练习 */
@Component({
  selector: 'app-student-analysis',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzEmptyModule,
    NzTooltipModule,
    SectionBannerComponent,
    MasteryBarComponent,
  ],
  templateUrl: './student-analysis.component.html',
  styleUrls: ['./student-analysis.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAnalysisComponent implements OnInit {
  private readonly service = inject(LearningAnalyticsService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly data = signal<StudentAnalysisDashboard | null>(null);
  readonly activeTab = signal<AnalysisTab>('weakness');
  readonly usingSampleData = this.service.usingSampleData;

  readonly tabs: { key: AnalysisTab; label: string; icon: string }[] = [
    { key: 'weakness', label: '我的薄弱点', icon: 'exclamation-circle' },
    { key: 'path', label: '学习路径', icon: 'deployment-unit' },
    { key: 'messages', label: '消息', icon: 'bell' },
    { key: 'practice', label: '专项练习', icon: 'edit' },
  ];

  readonly overview = computed(() => this.data()?.overview ?? null);
  readonly knowledgePoints = computed(() => this.overview()?.knowledgePoints ?? []);
  readonly losses = computed(() => this.overview()?.losses ?? []);

  /** 是否有可展示的学情数据（有作答证据 / 成绩 / 失分归因） */
  readonly hasWeaknessData = computed(() => {
    const o = this.overview();
    if (!o) return false;
    return o.knowledgePoints.length > 0 || o.score != null || o.losses.length > 0;
  });
  readonly path = computed(() => this.data()?.path ?? []);
  readonly reviewProgress = computed(() => this.data()?.reviewProgress ?? null);
  readonly notifications = computed(() => this.data()?.notifications ?? []);
  readonly unreadCount = computed(() => this.data()?.unreadCount ?? 0);
  readonly practices = computed(() => this.data()?.practices ?? []);

  readonly reviewPercent = computed(() => {
    const p = this.reviewProgress();
    if (!p || !p.total) return 0;
    return Math.round((p.completed / p.total) * 100);
  });

  ngOnInit(): void {
    this.service.getStudentDashboard().subscribe({
      next: d => {
        this.data.set(d);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  setTab(tab: AnalysisTab): void {
    this.activeTab.set(tab);
  }

  /** 未读消息数（Tab 徽标） */
  readonly unreadBadge = computed(() => {
    const n = this.unreadCount();
    return n > 0 ? n : 0;
  });

  markRead(n: AnalysisNotification): void {
    if (n.isRead) return;
    n.isRead = true;
    this.data.update(d => (d ? { ...d, unreadCount: Math.max(0, d.unreadCount - 1) } : d));
    this.service.markNotificationRead(n.id).subscribe({ error: () => void 0 });
  }

  /** 开始专项练习：进入「专项练习」页（推荐题集 → 作答 → 掌握率回写） */
  startPractice(p: PracticeItem): void {
    this.router.navigate(['/student/practice'], {
      queryParams: {
        courseId: p.courseId,
        knowledgePointId: p.knowledgePointId || undefined,
        kpName: p.title,
      },
    });
  }

  /** 开始针对练习（失分归因卡按钮） */
  startTargetedPractice(): void {
    this.setTab('practice');
  }

  /** 无数据时引导去课程中心练习 */
  goCourses(): void {
    this.router.navigate(['/student/courses']);
  }

  levelClass(level: MasteryLevel): string {
    return level === MasteryLevel.Weak ? 'weak' : level === MasteryLevel.Fair ? 'fair' : 'ok';
  }

  /** 相对时间：刚刚 / N 分钟前 / N 小时前 / 昨天 / N 天前 */
  relativeTime(iso: string): string {
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
