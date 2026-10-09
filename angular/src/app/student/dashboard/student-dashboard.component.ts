import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { LearningService } from '../../proxy/learning/learning.service';
import { StudentHeroComponent, type StudentHeroStat } from '../shared/student-hero/student-hero.component';
import type { LearningDashboardDto, StudentCourseListItemDto } from '../../proxy/learning/dtos/models';

interface RhythmDay {
  label: string;
  minutes: number;
  percent: number;
}

interface GoalItem {
  label: string;
  current: number;
  target: number;
  percent: number;
}

/** 为你推荐入口（与原型「为你推荐」四宫格一致） */
interface QuickEntry {
  label: string;
  desc: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-student-dashboard',
  standalone: true,
  imports: [CommonModule, DecimalPipe, RouterModule, NzIconModule, NzSpinModule, StudentHeroComponent],
  templateUrl: './student-dashboard.component.html',
  styleUrls: ['./student-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentDashboardComponent implements OnInit {
  private readonly learningService = inject(LearningService);
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);

  readonly loading = signal(true);
  readonly dashboard = signal<LearningDashboardDto | null>(null);
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);
  readonly userName = signal('同学');

  /** 顶部标题栏统计（对齐原型 metrics 四项） */
  readonly heroStats = computed<StudentHeroStat[]>(() => {
    const d = this.dashboard();
    const hours = Math.round(((d?.totalLearningTime || 0) / 60) * 10) / 10;
    return [
      { label: '在学课程', value: d?.inProgressCourses || 0, suffix: '门', icon: 'read', color: '#087f7a' },
      { label: '学习时长', value: hours, suffix: 'h', icon: 'clock-circle', color: '#3f7fa8' },
      { label: '已完成', value: d?.completedCourses || 0, suffix: '门', icon: 'check-circle', color: '#22c55e' },
      { label: '习题练习', value: d?.totalExerciseRecords || 0, suffix: '次', icon: 'form', color: '#b0803a' },
    ];
  });

  /** 继续上次的学习：进行中的课程（按进度降序，最多 4 条） */
  readonly continueCourses = computed<StudentCourseListItemDto[]>(() => {
    const list = (this.myCourses() || []).filter(
      c => c.status === 1 || ((c.progress || 0) > 0 && (c.progress || 0) < 100)
    );
    return list
      .slice()
      .sort((a, b) => (b.progress || 0) - (a.progress || 0))
      .slice(0, 4);
  });

  /** 学习节奏：最近 7 天学习时长（分钟 → 相对高度） */
  readonly rhythm = computed<RhythmDay[]>(() => {
    const d = this.dashboard();
    const labels = d?.dailyTimeLabels || [];
    const values = d?.dailyTimeValues || [];
    const max = Math.max(1, ...values);
    return labels.map((l, i) => {
      const minutes = values[i] || 0;
      return { label: this.shortDay(l), minutes, percent: Math.round((minutes / max) * 100) };
    });
  });

  /** 7 天累计学习分钟 */
  readonly rhythmTotal = computed(() => this.rhythm().reduce((s, d) => s + d.minutes, 0));

  /** 本月目标（沿用真实统计，目标值为演示口径） */
  readonly goals = computed<GoalItem[]>(() => {
    const d = this.dashboard();
    const completed = d?.completedCourses || 0;
    const exercises = d?.totalExerciseRecords || 0;
    const courseTarget = 3;
    const exerciseTarget = 200;
    return [
      {
        label: `完成 ${courseTarget} 门课程`,
        current: completed,
        target: courseTarget,
        percent: Math.min(100, Math.round((completed / courseTarget) * 100)),
      },
      {
        label: `完成 ${exerciseTarget} 道习题`,
        current: exercises,
        target: exerciseTarget,
        percent: Math.min(100, Math.round((exercises / exerciseTarget) * 100)),
      },
    ];
  });

  /** 为你推荐入口 */
  readonly quickEntries: QuickEntry[] = [
    { label: '课程资源', desc: '课件 · 视频 · 资料', icon: 'folder-open', route: '/student/resources' },
    { label: '智能答疑', desc: 'AI 学习助手', icon: 'robot', route: '/student/ai/chat' },
    { label: '实训中心', desc: '项目 · 仿真 · 报告', icon: 'experiment', route: '/student/practicums' },
    { label: '就业机会', desc: '职位 · 投递 · 去向', icon: 'idcard', route: '/student/employment/jobs' },
  ];

  get isLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  ngOnInit(): void {
    const cu = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    const name = cu?.['userName'];
    if (typeof name === 'string' && name.trim()) this.userName.set(name.trim());

    this.learningService.getDashboard().subscribe({
      next: data => {
        this.dashboard.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.learningService.getMyCourses().subscribe({
      next: list => this.myCourses.set(list || []),
      error: () => this.myCourses.set([]),
    });
  }

  /** 把后端返回的日期标签压缩为「周x」或「M/D」 */
  private shortDay(label: string): string {
    if (!label) return '';
    // 形如 2026-10-07 → 10/07
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(label);
    if (m) return `${Number(m[2])}/${Number(m[3])}`;
    return label.length > 5 ? label.slice(-5) : label;
  }

  /** 目标进度条宽度（避免 0 宽度时看不出轨道） */
  goalWidth(percent: number): string {
    return `${Math.max(percent, 2)}%`;
  }
}
