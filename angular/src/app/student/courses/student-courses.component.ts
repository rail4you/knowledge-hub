import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { AuthService, Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { CourseService } from '../../proxy/courses/course.service';
import { MajorService } from '../../proxy/majors/major.service';
import { LearningService } from '../../proxy/learning/learning.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { LearningDashboardDto, StudentCourseListItemDto } from '../../proxy/learning/dtos/models';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';
import { hashGradient } from '../../shared/utils/color.util';
import { VoiceContextService } from '../voice/voice-context.service';

interface StatItem {
  label: string;
  value: number;
  suffix: string;
  icon: string;
  color: string;
}

interface MajorChip {
  id: string | null;
  name: string;
  icon: string;
  color: string;
}

interface DifficultyChip {
  value: number | null;
  label: string;
  icon: string;
}

interface HotCourse {
  rank: number;
  title: string;
  students: number;
  color: string;
}

@Component({
  selector: 'app-student-courses',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzProgressModule,
    NzEmptyModule,
    NzTooltipModule,
    NzSelectModule,
    StudentHeroComponent,
  ],
  templateUrl: './student-courses.component.html',
  styleUrls: ['./student-courses.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentCoursesComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
  private readonly majorService = inject(MajorService);
  private readonly restService = inject(RestService);
  private readonly learningService = inject(LearningService);
  private readonly authService = inject(AuthService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly voiceContext = inject(VoiceContextService);

  readonly loading = signal(false);

  readonly courses = signal<CourseDto[]>([]);
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);
  readonly dashboard = signal<LearningDashboardDto | null>(null);
  /** 当前 published 查询的后端 totalCount（分页总数，避免只用 items.length 截断） */
  readonly publishedTotal = signal<number | null>(null);

  readonly filter = signal('');
  readonly selectedMajor = signal<string | null>(null);
  readonly selectedDifficulty = signal<number | null>(null);
  readonly selectedStatus = signal<string | null>('enrolled');

  /** 视图模型：所有课程（如果选了"已选课"则仅显示我的课程） */
  readonly visibleCourses = computed<CourseDto[]>(() => {
    const status = this.selectedStatus();
    if (status === 'enrolled') {
      const my = this.myCourses();
      const myIds = new Set(my.map(c => c.courseId));
      const published = this.courses().filter(c => myIds.has(c.id) || c.isEnrolled);
      const publishedIds = new Set(published.map(c => c.id));
      // 口径统一：published 接口按租户/状态/分页过滤，会漏掉已选但未发布、跨租户或翻页外的课程，
      // 导致“我的课程”列表数（7）小于统计数（10）。把缺失的已选课按 myCourses 补齐，
      // 并叠加当前搜索/专业筛选（难度未知时不按难度剔除），保证列表数 == 统计数。
      const keyword = (this.filter() || '').trim().toLowerCase();
      const majorSel = this.selectedMajor();
      const missing: CourseDto[] = [];
      for (const m of my) {
        if (!m.courseId || publishedIds.has(m.courseId)) continue;
        if (keyword && !(m.courseTitle || '').toLowerCase().includes(keyword)) continue;
        if (majorSel === this.majorPublicOnlyValue) {
          if (m.majorId) continue;
        } else if (majorSel) {
          if (m.majorId !== majorSel) continue;
        }
        missing.push({
          id: m.courseId,
          title: m.courseTitle || '未命名课程',
          coverImageUrl: m.courseCoverImageUrl ?? null,
          majorId: m.majorId ?? null,
          majorName: m.majorName ?? null,
          majorIds: m.majorId ? [m.majorId] : [],
          majorNames: m.majorName ? [m.majorName] : [],
          semester: m.semester ?? null,
          credits: m.credits ?? null,
          isEnrolled: true,
          progress: m.progress ?? 0,
          studentCount: 0,
          chapterCount: 0,
        } as unknown as CourseDto);
      }
      return [...published, ...missing];
    }
    if (status === 'recommended') {
      // 后端已按 isRecommended 过滤；此处再兜底一次，兼容旧数据/缓存
      return this.courses().filter(c => (c as any).isRecommended);
    }
    return this.courses();
  });

  /**
   * Hero 统计：随 tab/列表同步，口径与 visibleCourses() 一致。
   * - 第一项显示当前列表数（我的课程 / 全部课程 / 推荐课程随 tab 切换 label）
   * - 学习中/已完成/平均进度按当前可见列表中已选课的子集计算
   */
  readonly stats = computed<StatItem[]>(() => {
    const status = this.selectedStatus();
    const visible = this.visibleCourses();
    const myMap = new Map((this.myCourses() || []).map(c => [c.courseId as string, c]));
    const enrolledVisible = visible.filter(v => (v.id && myMap.has(v.id)) || (v as any).isEnrolled);
    const inProgress = enrolledVisible.filter(v => myMap.get(v.id!)?.status === 1).length;
    const completed = enrolledVisible.filter(v => myMap.get(v.id!)?.status === 2).length;
    const avgProgress = enrolledVisible.length
      ? Math.round(enrolledVisible.reduce((s, v) => s + (myMap.get(v.id!)?.progress || 0), 0) / enrolledVisible.length)
      : 0;
    const firstLabel = status === 'enrolled' ? '已选课程' : status === 'recommended' ? '推荐课程' : '全部课程';
    // 全部/推荐 tab 下第一项用后端 totalCount 更准（当前页可能截断），有筛选时用可见数
    const firstValue =
      !this.hasActiveFilters() && status !== 'enrolled' && this.publishedTotal() != null
        ? this.publishedTotal()!
        : visible.length;
    return [
      { label: firstLabel, value: firstValue, suffix: '门', icon: 'book', color: '#0f766e' },
      { label: '学习中', value: inProgress, suffix: '门', icon: 'play-circle', color: '#14b8a6' },
      { label: '已完成', value: completed, suffix: '门', icon: 'check-circle', color: '#16a34a' },
      { label: '学习进度', value: avgProgress, suffix: '%', icon: 'rise', color: '#0d5e56' },
    ];
  });

  readonly majors = signal<MajorChip[]>([
    { id: null, name: '全部专业', icon: 'appstore', color: '#0f766e' },
    { id: '__public__', name: '公共课', icon: 'bank', color: '#5b93db' },
  ]);

  /** 专业下拉哨兵值：选中表示只看公共课（无专业归属） */
  readonly majorPublicOnlyValue = '__public__';

  readonly difficulties: DifficultyChip[] = [
    { value: null, label: '全部难度', icon: 'appstore' },
    { value: 1, label: '入门', icon: 'flag' },
    { value: 2, label: '初级', icon: 'signal' },
    { value: 3, label: '中级', icon: 'thunderbolt' },
    { value: 4, label: '高级', icon: 'rocket' },
  ];

  readonly statusFilters = [
    { value: 'enrolled', label: '我的课程', icon: 'user' },
    { value: 'all', label: '全部课程', icon: 'appstore' },
    { value: 'recommended', label: '推荐课程', icon: 'star' },
  ];

  /** 热门课程（基于已选学生数排序，mock） */
  readonly hotCourses = signal<HotCourse[]>([
    { rank: 1, title: 'Python 程序设计基础', students: 2840, color: '#0f766e' },
    { rank: 2, title: '高等数学（上）', students: 2654, color: '#0d5e56' },
    { rank: 3, title: '大学英语（一）', students: 2320, color: '#14b8a6' },
    { rank: 4, title: '计算机网络原理', students: 1987, color: '#16a34a' },
    { rank: 5, title: '机械制图与 CAD', students: 1854, color: '#5b93db' },
  ]);

  ngOnInit(): void {
    this.voiceContext.register('courses', () => {
      const list = this.visibleCourses();
      const stats = this.stats();
      const head = list.slice(0, 10);
      const lines = head.map(
        (c, i) => `第${i + 1}门：${c.title || '未命名'}，${this.courseMajorText(c)}，${this.getMyProgress(c.id!) > 0 ? `进度${Math.round(this.getMyProgress(c.id!))}%` : this.isEnrolled(c.id!) ? '已选课未开始' : '未选课'}。`
      );
      return {
        key: 'courses',
        route: this.router.url,
        title: '课程中心',
        summary:
          `课程中心。${stats.map(s => `${s.label}${s.value}${s.suffix}`).join('，')}。` +
          `当前列表共${list.length}门。${lines.join('')}`,
        items: list.slice(0, 10).map(c => ({ id: c.id!, title: c.title || '未命名课程' })),
      };
    });
    this.loadCourses();
    this.loadMajors();
    this.loadMyCourses();
    this.loadDashboard();
  }

  ngOnDestroy(): void {
    this.voiceContext.unregister('courses');
  }

  loadCourses(): void {
    this.loading.set(true);
    // 'enrolled' 是客户端基于 myCourses() 过滤，不发请求
    // 'recommended' 必须带上 isRecommended=true；注意不能走 CourseService.getPublished，
    // 生成的代理只透传固定字段会把 isRecommended 丢掉，这里直接调 REST 接口。
    const status = this.selectedStatus();
    const majorSel = this.selectedMajor();
    const params: any = {
      filter: this.filter() || undefined,
      difficulty: this.selectedDifficulty() ?? undefined,
      skipCount: 0,
      // 列表无分页 UI，取足够大的页避免 totalCount 与 items.length 不一致（曾因截断出现 8 vs 10）
      maxResultCount: 200,
    };
    if (majorSel === this.majorPublicOnlyValue) {
      params.onlyPublicCourses = true;
    } else if (majorSel) {
      params.majorId = majorSel;
    }
    if (status === 'recommended') {
      params.isRecommended = true;
    }
    this.restService
      .request<any, PagedResultDto<CourseDto>>(
        { method: 'GET', url: '/api/app/course/published', params },
        { apiName: 'KnowledgeHub' } as Partial<Rest.Config>
      )
      .subscribe({
        next: result => {
          this.courses.set(result.items || []);
          this.publishedTotal.set(result.totalCount ?? (result.items || []).length);
          this.loading.set(false);
        },
        error: () => {
          this.publishedTotal.set(null);
          this.loading.set(false);
          this.message.error('课程加载失败');
        },
      });
  }

  loadMyCourses(): void {
    if (!this.authService.isAuthenticated) return;
    this.learningService.getMyCourses().subscribe({
      next: list => {
        this.myCourses.set((list || []) as StudentCourseListItemDto[]);
      },
      error: () => {
        // 静默失败，不影响主流程
      },
    });
  }

  loadDashboard(): void {
    if (!this.authService.isAuthenticated) return;
    this.learningService.getDashboard().subscribe({
      next: data => {
        this.dashboard.set(data);
      },
      error: () => {
        // 静默失败
      },
    });
  }

  /** 专业下拉选项：全量专业字典（不受当前课程列表影响），前面保留“全部专业/公共课” */
  loadMajors(): void {
    this.majorService.getLookupList().subscribe({
      next: list => {
        const colorPalette = ['#0f766e', '#14b8a6', '#0d5e56', '#16a34a', '#5b93db', '#8b5cf6'];
        const chips: MajorChip[] = [
          { id: null, name: '全部专业', icon: 'appstore', color: '#0f766e' },
          { id: this.majorPublicOnlyValue, name: '公共课', icon: 'bank', color: '#5b93db' },
        ];
        (list || []).forEach((m, i) => {
          if (!m?.id) return;
          chips.push({ id: m.id, name: m.name || '未命名专业', icon: 'book', color: colorPalette[i % colorPalette.length] });
        });
        this.majors.set(chips);
        // 当前选中的专业若已不在字典中（如被删除），回落到全部
        const sel = this.selectedMajor();
        if (sel && sel !== this.majorPublicOnlyValue && !chips.some(c => c.id === sel)) {
          this.selectedMajor.set(null);
        }
      },
      error: () => {
        // 字典加载失败时保留默认两项，不影响主流程
      },
    });
  }

  /** 课程卡片的专业文本：多专业用“、”连接，无归属显示“公共课” */
  courseMajorText(c: CourseDto | null | undefined): string {
    const names = (c?.majorNames || []).filter(n => !!n);
    if (names.length > 0) return names.join('、');
    return (c?.majorName || '').trim() || '公共课';
  }

  /** 是否有生效中的筛选（搜索 / 专业 / 难度任一非默认） */
  hasActiveFilters(): boolean {
    return !!(this.filter() || this.selectedMajor() || this.selectedDifficulty() !== null);
  }

  selectMajor(id: string | null) {
    this.selectedMajor.set(id);
    this.loadCourses();
  }

  selectDifficulty(value: number | null) {
    this.selectedDifficulty.set(value);
    this.loadCourses();
  }

  selectStatus(value: string) {
    this.selectedStatus.set(value);
    // 'enrolled' 是基于 myCourses() 的客户端过滤，不需要重新发请求
    // 'all' / 'recommended' 需要重新拉取，否则点击"全部课程/推荐课程"看上去无反应
    if (value !== 'enrolled') {
      this.loadCourses();
    }
  }

  onSearch() {
    this.loadCourses();
  }

  onReset() {
    this.filter.set('');
    this.selectedMajor.set(null);
    this.selectedDifficulty.set(null);
    this.loadCourses();
  }

  openCourse(id: string) {
    this.router.navigate(['/student/courses', id]);
  }

  enroll(course: CourseDto, event: Event) {
    // 学生端不允许自助选课：选课只能由老师分配。此处仅提示，不调用后端。
    event.stopPropagation();
    this.message.warning('未选课，请联系老师分配课程');
  }

  startLearning(course: CourseDto | StudentCourseListItemDto, event: Event) {
    event.stopPropagation();
    const id = (course as any).courseId ?? course.id;
    this.router.navigate(['/student/courses', id]);
  }

  /** 课程封面渐变（基于专业+标题 hash） */
  courseGradient(course: CourseDto | { title?: string; id?: string; majorName?: string }): string {
    return this.gradientByKey(
      course?.title || course?.id || 'x',
      course?.majorName || ''
    );
  }

  gradientByKey(primary: string, secondary: string): string {
    const palettes = [
      '#0f766e',
      '#0d5e56',
      '#14b8a6',
      '#16a34a',
      '#5b93db',
      '#059669',
      '#0d9488',
      '#d97706',
    ];
    const key = (primary || 'x') + (secondary || '');
    return hashGradient(key, palettes);
  }

  hasCover(course: CourseDto): boolean {
    return !!course.coverImageUrl && course.coverImageUrl.trim().length > 0;
  }

  difficultyLabel(d: number | null | undefined): string {
    const labels = ['入门', '初级', '中级', '高级', '专家'];
    return labels[(d || 1) - 1] || '未设置';
  }

  difficultyColor(d: number | null | undefined): string {
    const colors = ['#34d399', '#22c55e', '#0f766e', '#0d5e56', '#14b8a6'];
    return colors[(d || 1) - 1] || '#94a3b8';
  }

  getMyProgress(courseId: string): number {
    const found = this.myCourses().find(c => c.courseId === courseId);
    return found?.progress || 0;
  }

  isEnrolled(courseId: string): boolean {
    return this.myCourses().some(c => c.courseId === courseId);
  }

  isRecommended(course: CourseDto): boolean {
    return !!(course as any).isRecommended;
  }
}
