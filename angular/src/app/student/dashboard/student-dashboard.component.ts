import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { LearningService } from '../../proxy/learning/learning.service';
import type { LearningDashboardDto, RecentLearningDto, StudentCourseListItemDto } from '../../proxy/learning/dtos/models';

/** 顶部指标卡（对齐原型 metrics 四项） */
interface MetricItem {
  label: string;
  value: string;
  suffix: string;
  icon: string;
  hint: string;
}

/** 日历单元格（空白 or 日期） */
interface CalendarCell {
  day: number | null;
  isToday: boolean;
}

/** 为你推荐入口（对齐原型「为你推荐」四宫格） */
interface QuickEntry {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-student-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, NzIconModule, NzSpinModule],
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

  private readonly now = new Date();

  /** 页头日期标签：2026 年 10 月 12 日 · 星期一 */
  readonly todayLabel = (() => {
    const d = this.now;
    const week = ['日', '一', '二', '三', '四', '五', '六'][d.getDay()];
    return `${d.getFullYear()} 年 ${d.getMonth() + 1} 月 ${d.getDate()} 日 · 星期${week}`;
  })();

  /** 学习日历副标题：2026 年 10 月 */
  readonly calendarTitle = `${this.now.getFullYear()} 年 ${this.now.getMonth() + 1} 月`;

  /** 学习日历（周一为一周起点，与原型「一二三四五六日」一致） */
  readonly calendarCells = computed<CalendarCell[]>(() => {
    const year = this.now.getFullYear();
    const month = this.now.getMonth();
    const today = this.now.getDate();
    const firstWeekday = (new Date(year, month, 1).getDay() + 6) % 7; // 周一=0
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const cells: CalendarCell[] = [];
    for (let i = 0; i < firstWeekday; i++) cells.push({ day: null, isToday: false });
    for (let day = 1; day <= daysInMonth; day++) cells.push({ day, isToday: day === today });
    return cells;
  });

  /** 连续学习天数：从最近一天（含）向前回溯非零记录；今天尚无记录时从昨天起算 */
  readonly streakDays = computed(() => {
    const values = this.dashboard()?.dailyTimeValues || [];
    if (!values.length) return 0;
    let i = values.length - 1;
    if (values[i] === 0) i--;
    let streak = 0;
    while (i >= 0 && values[i] > 0) {
      streak++;
      i--;
    }
    return streak;
  });

  /** 顶部标题栏统计（对齐原型 metrics：在学课程 / 学习时长 / 已完成课程 / 学习连续天数） */
  readonly metrics = computed<MetricItem[]>(() => {
    const d = this.dashboard();
    const hours = Math.round(((d?.totalLearningTime || 0) / 60) * 10) / 10;
    const remaining = Math.max(0, (d?.totalCourses || 0) - (d?.completedCourses || 0));
    return [
      { label: '在学课程', value: `${d?.inProgressCourses || 0}`, suffix: '门', icon: 'book', hint: '本学期持续成长' },
      { label: '学习时长', value: `${hours}`, suffix: 'h', icon: 'clock-circle', hint: '累计在线学习时长' },
      {
        label: '已完成课程',
        value: `${d?.completedCourses || 0}`,
        suffix: '门',
        icon: 'check-circle',
        hint: remaining > 0 ? `还有 ${remaining} 门课程进行中` : '继续保持，稳步向前',
      },
      {
        label: '学习连续天数',
        value: `${this.streakDays()}`,
        suffix: '天',
        icon: 'line-chart',
        hint: '保持自己的学习节奏',
      },
    ];
  });

  /** 继续上次的学习：进行中的课程（按进度降序，最多 3 条，对齐原型） */
  readonly continueCourses = computed<StudentCourseListItemDto[]>(() => {
    const list = (this.myCourses() || []).filter(
      c => c.status === 1 || ((c.progress || 0) > 0 && (c.progress || 0) < 100)
    );
    return list
      .slice()
      .sort((a, b) => (b.progress || 0) - (a.progress || 0))
      .slice(0, 3);
  });

  /** 最近学习（对齐原型右栏 list-row：待办 / 动态提醒，用真实学习记录呈现） */
  readonly recentItems = computed<RecentLearningDto[]>(() => {
    const list = this.dashboard()?.recentLearning || [];
    return list.slice(0, 3);
  });

  /** 为你推荐入口（对齐原型四宫格：课程资源 / 智能答疑 / 实训中心 / 就业机会） */
  readonly quickEntries: QuickEntry[] = [
    { label: '课程资源', icon: 'folder-open', route: '/student/resources' },
    { label: '智能答疑', icon: 'thunderbolt', route: '/student/ai/chat' },
    { label: '实训中心', icon: 'experiment', route: '/student/practicums' },
    { label: '就业机会', icon: 'briefcase', route: '/student/employment/jobs' },
  ];

  get isLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  /** 欢迎卡「继续学习」：优先跳转最近进行中课程的学习页 */
  get continueLearningRoute(): string[] {
    const c = this.continueCourses()[0];
    return c?.courseId ? ['/student/courses', c.courseId, 'learn'] : ['/student/courses'];
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

  /** 课程行副标题：专业 · 进度 N% · 习题 x/y（真实数据） */
  courseSubtitle(c: StudentCourseListItemDto): string {
    const parts = [c.majorName || '公共课', `进度 ${c.progress || 0}%`];
    if (c.totalExerciseCount) parts.push(`习题 ${c.completedExerciseCount || 0}/${c.totalExerciseCount}`);
    return parts.join(' · ');
  }

  /** 最近学习行：10 月 7 日 14:30 · 进度 42% */
  recentSubtitle(r: RecentLearningDto): string {
    const parts: string[] = [];
    if (r.lastAccessAt) {
      const d = new Date(r.lastAccessAt);
      if (!isNaN(d.getTime())) {
        const hh = `${d.getHours()}`.padStart(2, '0');
        const mm = `${d.getMinutes()}`.padStart(2, '0');
        parts.push(`${d.getMonth() + 1} 月 ${d.getDate()} 日 ${hh}:${mm}`);
      }
    }
    parts.push(`进度 ${r.progress || 0}%`);
    return parts.join(' · ');
  }

  /** 课程学习页路由 */
  learnRoute(c: StudentCourseListItemDto): string[] {
    return c.courseId ? ['/student/courses', c.courseId, 'learn'] : ['/student/courses'];
  }

  /** 课程详情路由 */
  courseRoute(c: StudentCourseListItemDto): string[] {
    return c.courseId ? ['/student/courses', c.courseId] : ['/student/courses'];
  }

  /** 最近学习课程详情路由 */
  recentRoute(r: RecentLearningDto): string[] {
    return r.courseId ? ['/student/courses', r.courseId] : ['/student/courses'];
  }
}
