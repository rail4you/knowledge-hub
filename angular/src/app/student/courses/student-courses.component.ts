import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { AuthService, Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { CourseService } from '../../proxy/courses/course.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { CourseStatus } from '../../proxy/courses/enums/course-status.enum';
import type { CourseDto, StudentCourseDto } from '../../proxy/courses/dtos/models';
import type { LearningDashboardDto } from '../../proxy/learning/dtos/models';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';
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
    NzButtonModule,
    NzSpinModule,
    NzProgressModule,
    NzEmptyModule,
    NzTooltipModule,
    StudentHeroComponent,
  ],
  templateUrl: './student-courses.component.html',
  styleUrls: ['./student-courses.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentCoursesComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
  private readonly restService = inject(RestService);
  private readonly learningService = inject(LearningService);
  private readonly authService = inject(AuthService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly voiceContext = inject(VoiceContextService);

  readonly loading = signal(false);
  readonly enrolling = signal<string | null>(null);

  readonly courses = signal<CourseDto[]>([]);
  readonly myCourses = signal<StudentCourseDto[]>([]);
  readonly dashboard = signal<LearningDashboardDto | null>(null);

  readonly filter = signal('');
  readonly selectedMajor = signal<string | null>(null);
  readonly selectedDifficulty = signal<number | null>(null);
  readonly selectedStatus = signal<string | null>('enrolled');

  readonly CourseStatus = CourseStatus;

  /** 视图模型：所有课程（如果选了"已选课"则仅显示我的课程） */
  readonly visibleCourses = computed<CourseDto[]>(() => {
    const status = this.selectedStatus();
    if (status === 'enrolled') {
      const myIds = new Set(this.myCourses().map(c => c.courseId));
      return this.courses().filter(c => myIds.has(c.id) || c.isEnrolled);
    }
    if (status === 'recommended') {
      // 后端已按 isRecommended 过滤；此处再兜底一次，兼容旧数据/缓存
      return this.courses().filter(c => (c as any).isRecommended);
    }
    return this.courses();
  });

  /** 我的课程（从 my-courses 服务取得） */
  readonly myCourseModels = computed<StudentCourseDto[]>(() => {
    return this.myCourses();
  });

  readonly stats = signal<StatItem[]>([
    { label: '已选课程', value: 0, suffix: '门', icon: 'book', color: '#0f766e' },
    { label: '学习中', value: 0, suffix: '门', icon: 'play-circle', color: '#14b8a6' },
    { label: '已完成', value: 0, suffix: '门', icon: 'check-circle', color: '#16a34a' },
    { label: '学习进度', value: 0, suffix: '%', icon: 'rise', color: '#0d5e56' },
  ]);

  readonly majors = signal<MajorChip[]>([
    { id: null, name: '全部专业', icon: 'appstore', color: '#0f766e' },
  ]);

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
        (c, i) => `第${i + 1}门：${c.title || '未命名'}，${c.majorName || '专业未知'}，${this.getMyProgress(c.id!) > 0 ? `进度${Math.round(this.getMyProgress(c.id!))}%` : this.isEnrolled(c.id!) ? '已选课未开始' : '未选课'}。`
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
    const params: any = {
      filter: this.filter() || undefined,
      majorId: this.selectedMajor() || undefined,
      difficulty: this.selectedDifficulty() ?? undefined,
      skipCount: 0,
      maxResultCount: 30,
    };
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
          this.loading.set(false);
          this.syncMajorChips(result.items || []);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('课程加载失败');
        },
      });
  }

  loadMyCourses(): void {
    if (!this.authService.isAuthenticated) return;
    this.learningService.getMyCourses().subscribe({
      next: list => {
        this.myCourses.set(list || []);
        this.updateStats();
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
        this.updateStats();
      },
      error: () => {
        // 静默失败
      },
    });
  }

  private updateStats() {
    const dash = this.dashboard();
    const my = this.myCourses();
    const totalCourses = my.length || 0;
    const inProgress = my.filter(c => c.status === 1).length;
    const completed = dash?.completedCourses ?? my.filter(c => c.status === 2).length;
    const avgProgress = Math.round(
      (dash?.averageProgress ??
        (my.length ? my.reduce((s, c) => s + (c.progress || 0), 0) / my.length : 0)) || 0
    );

    this.stats.set([
      { label: '已选课程', value: totalCourses, suffix: '门', icon: 'book', color: '#0f766e' },
      { label: '学习中', value: inProgress, suffix: '门', icon: 'play-circle', color: '#14b8a6' },
      { label: '已完成', value: completed, suffix: '门', icon: 'check-circle', color: '#16a34a' },
      { label: '学习进度', value: avgProgress, suffix: '%', icon: 'rise', color: '#0d5e56' },
    ]);
  }

  private syncMajorChips(items: CourseDto[]) {
    const map = new Map<string, string>(); // majorName → majorId
    items.forEach(c => {
      if (c.majorName && c.majorId && !map.has(c.majorName)) {
        map.set(c.majorName, c.majorId);
      }
    });
    const colorPalette = ['#0f766e', '#14b8a6', '#0d5e56', '#16a34a', '#5b93db', '#8b5cf6'];
    const chips: MajorChip[] = [{ id: null, name: '全部专业', icon: 'appstore', color: '#0f766e' }];
    let i = 0;
    Array.from(map.entries()).slice(0, 6).forEach(([name, id]) => {
      chips.push({ id, name, icon: 'book', color: colorPalette[i % colorPalette.length] });
      i++;
    });
    this.majors.set(chips);
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
    event.stopPropagation();
    if (this.enrolling()) return;
    this.enrolling.set(course.id);
    this.courseService.enroll(course.id).subscribe({
      next: () => {
        this.enrolling.set(null);
        this.message.success('选课成功，已添加到我的课程');
        // 标记为已选
        this.courses.update(list => list.map(c => c.id === course.id ? { ...c, isEnrolled: true } : c));
        this.loadMyCourses();
      },
      error: () => {
        this.enrolling.set(null);
        this.message.error('选课失败');
      },
    });
  }

  startLearning(course: CourseDto | StudentCourseDto, event: Event) {
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
    let hash = 0;
    for (let i = 0; i < key.length; i++) {
      hash = (hash * 31 + key.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
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
