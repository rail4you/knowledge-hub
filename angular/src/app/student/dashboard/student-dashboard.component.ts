import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { LearningService } from '../../proxy/learning/learning.service';
import { ClassroomAgentTaskService } from '../../proxy/teaching-agents/classroom-agent-task.service';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import { RecruitmentLiveService } from '../../proxy/recruitment-live-service/recruitment-live.service';
import { SchoolService } from '../../proxy/schools/school.service';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { ClassroomAgentAssignmentStatus } from '../../proxy/teaching-agents/enums/classroom-agent-assignment-status.enum';
import { StudentCourseStatus } from '../../proxy/learning/enums/student-course-status.enum';
import { PracticumEnrollmentStatus } from '../../proxy/practicums/enums/practicum-enrollment-status.enum';
import { RecruitmentLiveStatus } from '../../proxy/recruitment-live/recruitment-live-status.enum';
import { LiveType } from '../../proxy/recruitment-live/live-type.enum';
import type {
  LearningDashboardDto,
  RecentLearningDto,
  StudentCourseListItemDto,
} from '../../proxy/learning/dtos/models';
import type { StudentAgentTaskDto } from '../../proxy/teaching-agents/dtos/models';
import type { PracticumEnrollmentDto } from '../../proxy/practicums/dtos/models';
import type { RecruitmentLiveDto } from '../../proxy/recruitment-live/dtos/models';

/** 工作台统计卡（对齐原型 .db-stat） */
interface WorkbenchStat {
  label: string;
  value: number;
  route: string;
  hint: string;
}

/**
 * 工作台任务条目。
 *
 * 原型的「我的学习任务」是作业 + 实训。作业域（发布 / 提交 / 批阅 / 成果档案）
 * 后端尚未建设（见《学生端新原型改造方案》D3，已排期），因此这里用**已有的真实**
 * 两类任务填充：课堂任务（ClassroomAgentTask）+ 实训任务（PracticumEnrollment）。
 * 不伪造作业条目，也不用假数据凑数。
 */
interface WorkbenchTask {
  id: string;
  title: string;
  source: '课堂任务' | '实训任务';
  meta: string;
  route: string;
  status: '待完成' | '待批阅' | '已反馈';
  late: boolean;
}

/** 工具箱分组（对齐原型 dbStudentTools） */
interface ToolGroup {
  title: string;
  items: { label: string; route?: string; hint?: string }[];
}

/** 直播安排条目 */
interface LiveBrief {
  id: string;
  title: string;
  kind: string;
  start: string;
  status: string;
}

@Component({
  selector: 'app-student-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
  ],
  templateUrl: './student-dashboard.component.html',
  styleUrls: ['./student-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentDashboardComponent implements OnInit {
  private readonly learningService = inject(LearningService);
  private readonly agentTaskService = inject(ClassroomAgentTaskService);
  private readonly practicumService = inject(PracticumService);
  private readonly liveService = inject(RecruitmentLiveService);
  private readonly schoolService = inject(SchoolService);
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly dashboard = signal<LearningDashboardDto | null>(null);
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);
  readonly userName = signal('同学');
  readonly schoolName = signal('');
  readonly recentItems = signal<RecentLearningDto[]>([]);

  /** 任务筛选页签（对齐原型 db-tabs） */
  readonly taskTab = signal<'全部' | '待处理' | '已反馈'>('全部');
  /** 继续学习卡片搜索关键词 */
  readonly courseQuery = signal('');

  private readonly agentTasks = signal<StudentAgentTaskDto[]>([]);
  private readonly practicumEnrollments = signal<PracticumEnrollmentDto[]>([]);
  readonly lives = signal<LiveBrief[]>([]);

  /**
   * 全部学习工具：3 组 18 项（原型 dbStudentTools）。
   * 依赖尚未上线模块的条目（作业闭环 / 学习路径 / 成果档案 / 信任中心 /
   * 学生端知识图谱）**不渲染成假入口**，而是禁用并注明原因。
   */
  readonly toolGroups: ToolGroup[] = [
    {
      title: '学习与实践',
      items: [
        { label: '我的作业', hint: '作业闭环建设后开放' },
        { label: '课堂任务', route: '/student/agent-tasks' },
        { label: '我的实训', route: '/student/practicums/my' },
        { label: '直播与面试', route: '/student/recruitment-live' },
        { label: '薄弱点分析', route: '/student/analysis' },
        { label: '学习路径', hint: '学习路径建设后开放' },
      ],
    },
    {
      title: '资源与成长',
      items: [
        { label: '学习资源', route: '/student/resources' },
        { label: 'AI 学习助手', route: '/student/ai/chat' },
        { label: '微专业与证书', route: '/student/my-micro-majors' },
        { label: '成果档案', hint: '成果档案建设后开放' },
        { label: '知识图谱', hint: '学生端图谱页建设后开放' },
        { label: '个人中心', route: '/student/profile' },
      ],
    },
    {
      title: '求职与发展',
      items: [
        { label: '就业机会', route: '/student/employment/jobs' },
        { label: '我的简历', route: '/student/employment/my-resumes' },
        { label: '投递与面试', route: '/student/employment/my-applications' },
        { label: '求职工作台', hint: '求职工作台建设后开放' },
        { label: '资讯中心', route: '/student/news' },
        { label: '数据与 AI 规则', hint: '信任中心建设后开放' },
      ],
    },
  ];

  /** 常用工具（对齐原型 shortcuts） */
  readonly shortcuts: { label: string; desc: string; route: string; mark: string }[] = [
    { label: '课堂任务', desc: '完成智能体课堂任务', route: '/student/agent-tasks', mark: '任' },
    { label: '我的实训', desc: '完成项目实践', route: '/student/practicums/my', mark: '实' },
    { label: 'AI 学习助手', desc: '梳理学习思路', route: '/student/ai/chat', mark: 'AI' },
    { label: '微专业与证书', desc: '查看成长进度', route: '/student/my-micro-majors', mark: '证' },
  ];

  /** 合并后的学习任务（课堂任务 + 实训任务） */
  readonly tasks = computed<WorkbenchTask[]>(() => {
    const rows: WorkbenchTask[] = [];

    (this.agentTasks() || []).forEach(t => {
      rows.push({
        id: t.assignmentId || t.taskId || '',
        title: t.title || '课堂任务',
        source: '课堂任务',
        meta: t.teachingAgentName ? `智能体：${t.teachingAgentName}` : '课堂任务',
        route: '/student/agent-tasks',
        status: t.status === ClassroomAgentAssignmentStatus.Submitted ? '待批阅' : '待完成',
        late: !!t.dueTime && new Date(t.dueTime).getTime() < Date.now(),
      });
    });

    (this.practicumEnrollments() || []).forEach(e => {
      const hasScore = e.finalScore !== null && e.finalScore !== undefined;
      const status: WorkbenchTask['status'] = hasScore
        ? '已反馈'
        : e.status === PracticumEnrollmentStatus.Submitted
          ? '待批阅'
          : '待完成';
      rows.push({
        id: e.id || '',
        title: e.projectTitle || '实训任务',
        source: '实训任务',
        meta: `进度 ${e.progress ?? 0}%`,
        route: '/student/practicums/my',
        status,
        late: false,
      });
    });

    return rows;
  });

  /** 待处理任务数（待完成 + 待批阅） */
  readonly pendingCount = computed(() => this.tasks().filter(t => t.status !== '已反馈').length);
  /** 已反馈任务数 */
  readonly feedbackCount = computed(() => this.tasks().filter(t => t.status === '已反馈').length);
  /** 未结束的直播安排（lives 已取全量分页，因此是准确值） */
  readonly liveCount = computed(() => this.lives().length);
  /** 侧栏「近期安排」只展示最近 3 条；统计卡用的是全量 */
  readonly recentLives = computed(() => this.lives().slice(0, 3));
  /** 学习消息 = 最近学习动态 + 待处理任务（提示文案已说明构成） */
  readonly messageCount = computed(() => this.recentItems().length + this.pendingCount());

  /** 四张统计卡 */
  readonly stats = computed<WorkbenchStat[]>(() => [
    {
      label: '待完成任务',
      value: this.pendingCount(),
      route: '/student/agent-tasks',
      hint: '全部课堂任务与实训任务',
    },
    {
      label: '已反馈任务',
      value: this.feedbackCount(),
      route: '/student/practicums/my',
      hint: '全部课堂任务与实训任务',
    },
    { label: '直播安排', value: this.liveCount(), route: '/student/recruitment-live', hint: '本校全部未结束的安排' },
    { label: '学习消息', value: this.messageCount(), route: '/student/analysis', hint: '最近学习动态 + 待处理任务' },
  ]);

  /** 按页签筛选后的任务 */
  readonly visibleTasks = computed<WorkbenchTask[]>(() => {
    const tab = this.taskTab();
    const rows = this.tasks();
    if (tab === '待处理') return rows.filter(t => t.status !== '已反馈');
    if (tab === '已反馈') return rows.filter(t => t.status === '已反馈');
    return rows;
  });

  /** 继续学习：进行中的课程（按进度降序） */
  readonly continueCourses = computed<StudentCourseListItemDto[]>(() => {
    const list = (this.myCourses() || []).filter(
      c => c.status === StudentCourseStatus.InProgress || ((c.progress || 0) > 0 && (c.progress || 0) < 100),
    );
    const q = this.courseQuery().trim().toLowerCase();
    const filtered = q ? list.filter(c => (c.courseTitle || '').toLowerCase().includes(q)) : list;
    return filtered.slice().sort((a, b) => (b.progress || 0) - (a.progress || 0)).slice(0, 4);
  });

  /** 继续学习 CTA：优先跳转最近进行中课程 */
  readonly continueLearningRoute = computed<string[]>(() => {
    const c = this.continueCourses()[0];
    return c?.courseId ? ['/student/courses', c.courseId, 'learn'] : ['/student/courses'];
  });

  readonly continueCourseTitle = computed(() => this.continueCourses()[0]?.courseTitle || '课程中心');

  get isLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  ngOnInit(): void {
    const cu = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    const name = cu?.['userName'];
    if (typeof name === 'string' && name.trim()) this.userName.set(name.trim());

    // 机构中文名（租户 slug 不可直接展示）
    const tenant = this.configState.getDeep('currentTenant') as Record<string, unknown> | undefined;
    const tenantId = (tenant?.['id'] as string) || '';
    if (tenantId) {
      this.schoolService
        .getProfile(tenantId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
        next: profile => {
          const n = profile?.school?.name;
          if (n) this.schoolName.set(n);
        },
        error: () => undefined,
      });
    }

    this.learningService
      .getDashboard()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: data => {
          this.dashboard.set(data);
          this.recentItems.set((data?.recentLearning || []).slice(0, 3));
          this.loading.set(false);
        },
        error: () => {
          this.loadFailed.set(true);
          this.loading.set(false);
        },
      });

    this.learningService
      .getMyCourses()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: list => this.myCourses.set(list || []),
        error: () => this.myCourses.set([]),
      });

    // 课堂任务（学生视角：每行是本人的一条任务分配）
    // 统计卡要给出**准确数字**，所以这里把分页结果全部取完（上限 2000 条）——
    // 只取第一页就把「待完成任务」当总数展示会谎报数据。
    this.loadAllAgentTasks();

    // 我的实训（该接口返回全量，无分页参数）
    this.practicumService
      .getMyEnrollments()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: list => this.practicumEnrollments.set(list || []),
        error: () => this.practicumEnrollments.set([]),
      });

    // 直播安排：同样取完分页，「未结束的直播安排」才是准确值
    this.loadAllLives();
  }

  /** 课堂任务：翻页取全量（上限 2000 条），保证统计卡数字准确 */
  private loadAllAgentTasks(): void {
    const pageSize = 100;
    const maxPages = 20;
    const collected: StudentAgentTaskDto[] = [];
    let page = 0;

    const fetchPage = () => {
      this.agentTaskService
        .getStudentTaskList({ maxResultCount: pageSize, skipCount: page * pageSize })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: res => {
            const items = res?.items || [];
            collected.push(...items);
            const total = res?.totalCount ?? 0;
            if (items.length > 0 && collected.length < total && page < maxPages - 1) {
              page++;
              fetchPage();
            } else {
              this.agentTasks.set(collected);
            }
          },
          error: () => this.agentTasks.set(collected),
        });
    };

    fetchPage();
  }

  /** 直播安排：翻页取全量，再筛出未结束的 */
  private loadAllLives(): void {
    const pageSize = 100;
    const maxPages = 10;
    const collected: RecruitmentLiveDto[] = [];
    let page = 0;

    const fetchPage = () => {
      this.liveService
        .getStudentLives({ maxResultCount: pageSize, skipCount: page * pageSize })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: res => {
            const items = res?.items || [];
            collected.push(...items);
            const total = res?.totalCount ?? 0;
            if (items.length > 0 && collected.length < total && page < maxPages - 1) {
              page++;
              fetchPage();
            } else {
              this.lives.set(
                collected
                  .filter(l => l.status !== RecruitmentLiveStatus.Ended && l.status !== RecruitmentLiveStatus.Cancelled)
                  .map(l => ({
                    id: l.id || '',
                    title: l.title || '直播',
                    kind: l.type === LiveType.Course ? '课程直播' : '招聘直播',
                    start: l.scheduledAt ? this.formatDate(l.scheduledAt) : '',
                    status: l.statusText || (l.status === RecruitmentLiveStatus.Active ? '进行中' : '待开始'),
                  })),
              );
            }
          },
          error: () => this.lives.set([]),
        });
    };

    fetchPage();
  }

  /** 重新加载 */
  reload(): void {
    this.loadFailed.set(false);
    this.loading.set(true);
    this.ngOnInit();
  }

  /** 切换任务页签 */
  setTaskTab(tab: '全部' | '待处理' | '已反馈'): void {
    this.taskTab.set(tab);
  }

  /** 设置课程搜索关键词 */
  setCourseQuery(q: string): void {
    this.courseQuery.set(q);
  }

  /** 搜索表单提交（关键词已在 input 事件里同步，这里只需阻止默认跳转） */
  onSearch(event: Event): void {
    event.preventDefault();
  }

  /** 跳转课程中心 */
  navigateToCourses(): void {
    this.router.navigate(['/student/courses']);
  }

  /** 最近学习项跳转课程详情 */
  recentRoute(r: RecentLearningDto): string[] {
    return r.courseId ? ['/student/courses', r.courseId] : ['/student/courses'];
  }

  /** 课程行副标题：专业 · 进度 N% · 习题 x/y */
  courseSubtitle(c: StudentCourseListItemDto): string {
    const parts = [c.majorName || '公共课', `进度 ${c.progress || 0}%`];
    if (c.totalExerciseCount) parts.push(`习题 ${c.completedExerciseCount || 0}/${c.totalExerciseCount}`);
    return parts.join(' · ');
  }

  /** 任务状态标签样式 */
  taskStatusClass(t: WorkbenchTask): string {
    if (t.status === '已反馈') return 'db-status--done';
    if (t.late) return 'db-status--late';
    return 'db-status--pending';
  }

  /** 课程学习页路由 */
  learnRoute(c: StudentCourseListItemDto): string[] {
    return c.courseId ? ['/student/courses', c.courseId, 'learn'] : ['/student/courses'];
  }

  /** 课程详情路由 */
  courseRoute(c: StudentCourseListItemDto): string[] {
    return c.courseId ? ['/student/courses', c.courseId] : ['/student/courses'];
  }

  private formatDate(v: string): string {
    const d = new Date(v);
    if (isNaN(d.getTime())) return '';
    return `${d.getMonth() + 1} 月 ${d.getDate()} 日 ${`${d.getHours()}`.padStart(2, '0')}:${`${d.getMinutes()}`.padStart(2, '0')}`;
  }
}
