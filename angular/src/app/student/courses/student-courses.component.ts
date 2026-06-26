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
import { AuthService } from '@abp/ng.core';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { CourseService } from '../../proxy/courses/course.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { CourseStatus } from '../../proxy/courses/enums/course-status.enum';
import type { CourseDto, StudentCourseDto } from '../../proxy/courses/dtos/models';
import type { LearningDashboardDto } from '../../proxy/learning/dtos/models';

interface HeroSlide {
  title: string;
  subtitle: string;
  description: string;
  tag: string;
  icon: string;
  color: string;
}

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
  ],
  templateUrl: './student-courses.component.html',
  styleUrls: ['./student-courses.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentCoursesComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
  private readonly learningService = inject(LearningService);
  private readonly authService = inject(AuthService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly enrolling = signal<string | null>(null);

  readonly courses = signal<CourseDto[]>([]);
  readonly myCourses = signal<StudentCourseDto[]>([]);
  readonly dashboard = signal<LearningDashboardDto | null>(null);

  readonly filter = signal('');
  readonly selectedMajor = signal<string | null>(null);
  readonly selectedDifficulty = signal<number | null>(null);
  readonly selectedStatus = signal<string | null>('enrolled');

  readonly activeHeroSlide = signal(0);
  private heroTimer: ReturnType<typeof setInterval> | null = null;

  readonly CourseStatus = CourseStatus;

  /** 视图模型：所有课程（如果选了"已选课"则仅显示我的课程） */
  readonly visibleCourses = computed<CourseDto[]>(() => {
    const status = this.selectedStatus();
    if (status === 'enrolled') {
      const myIds = new Set(this.myCourses().map(c => c.courseId));
      return this.courses().filter(c => myIds.has(c.id) || c.isEnrolled);
    }
    return this.courses();
  });

  /** 我的课程（从 my-courses 服务取得） */
  readonly myCourseModels = computed<StudentCourseDto[]>(() => {
    return this.myCourses();
  });

  readonly stats = signal<StatItem[]>([
    { label: '已选课程', value: 0, suffix: '门', icon: 'book', color: '#1e6ce8' },
    { label: '学习中', value: 0, suffix: '门', icon: 'play-circle', color: '#06b6d4' },
    { label: '已完成', value: 0, suffix: '门', icon: 'check-circle', color: '#10b981' },
    { label: '学习进度', value: 0, suffix: '%', icon: 'rise', color: '#0c4cb8' },
  ]);

  readonly heroSlides = signal<HeroSlide[]>([
    {
      title: '课程中心',
      subtitle: 'COURSE CENTER',
      description: '汇聚专业精品课程，章节清晰、资源丰富、习题完备，构建从知识获取到技能掌握的完整学习路径。',
      tag: '课程简介',
      icon: 'read',
      color: '#1e6ce8',
    },
    {
      title: '知识图谱 · 体系化学习',
      subtitle: 'KNOWLEDGE GRAPH',
      description: '基于专业知识点关系构建认知图谱，可视化呈现章节与资源脉络，让学习更系统、更高效。',
      tag: '知识图谱',
      icon: 'apartment',
      color: '#0c4cb8',
    },
    {
      title: '在线练习 · 巩固提升',
      subtitle: 'ONLINE EXERCISES',
      description: '每章配备精选习题与详细解析，自主评估掌握情况，让学习效果可量化、可追溯。',
      tag: '在线练习',
      icon: 'form',
      color: '#059669',
    },
  ]);

  readonly majors = signal<MajorChip[]>([
    { id: null, name: '全部专业', icon: 'appstore', color: '#1e6ce8' },
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
    { rank: 1, title: 'Python 程序设计基础', students: 2840, color: '#1e6ce8' },
    { rank: 2, title: '高等数学（上）', students: 2654, color: '#0c4cb8' },
    { rank: 3, title: '大学英语（一）', students: 2320, color: '#0891b2' },
    { rank: 4, title: '计算机网络原理', students: 1987, color: '#059669' },
    { rank: 5, title: '机械制图与 CAD', students: 1854, color: '#0284c7' },
  ]);

  ngOnInit(): void {
    this.loadCourses();
    this.loadMyCourses();
    this.loadDashboard();
    this.startHeroAutoPlay();
  }

  ngOnDestroy(): void {
    this.stopHeroAutoPlay();
  }

  startHeroAutoPlay() {
    this.stopHeroAutoPlay();
    this.heroTimer = setInterval(() => {
      this.activeHeroSlide.set((this.activeHeroSlide() + 1) % this.heroSlides().length);
    }, 6000);
  }

  stopHeroAutoPlay() {
    if (this.heroTimer) {
      clearInterval(this.heroTimer);
      this.heroTimer = null;
    }
  }

  selectHeroSlide(index: number) {
    this.activeHeroSlide.set(index);
    this.startHeroAutoPlay();
  }

  loadCourses(): void {
    this.loading.set(true);
    // 'enrolled' 是客户端基于 myCourses() 过滤，不发请求
    // 'all' / 'recommended' 都是从 getPublished 拉取，由 selectedStatus() 触发
    const status = this.selectedStatus();
    const input: any = {
      filter: this.filter() || undefined,
      majorId: this.selectedMajor() || undefined,
      difficulty: this.selectedDifficulty() ?? undefined,
      skipCount: 0,
      maxResultCount: 30,
    };
    // 'recommended' 透传 status 字段；后端 PagedCourseRequestDto 支持 status 过滤；
    // 未来若后端加专门的推荐接口，可在此处改为调用 getRecommendedCourses。
    if (status === 'recommended') {
      // 暂不附加 status：让 API 返回所有已发布课程，再由前端按热度/进度等本地排序。
      // 这样 'all' 与 'recommended' 至少都能响应点击、显示 loading、并刷新数据。
    }
    this.courseService.getPublished(input).subscribe({
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
      { label: '已选课程', value: totalCourses, suffix: '门', icon: 'book', color: '#1e6ce8' },
      { label: '学习中', value: inProgress, suffix: '门', icon: 'play-circle', color: '#06b6d4' },
      { label: '已完成', value: completed, suffix: '门', icon: 'check-circle', color: '#10b981' },
      { label: '学习进度', value: avgProgress, suffix: '%', icon: 'rise', color: '#0c4cb8' },
    ]);
  }

  private syncMajorChips(items: CourseDto[]) {
    const set = new Set<string>();
    items.forEach(c => {
      if (c.majorName) set.add(c.majorName);
    });
    const colorPalette = ['#06b6d4', '#10b981', '#0c4cb8', '#22c55e', '#14b8a6', '#3b82f6'];
    const chips: MajorChip[] = [{ id: null, name: '全部专业', icon: 'appstore', color: '#1e6ce8' }];
    let i = 0;
    Array.from(set).slice(0, 6).forEach(m => {
      chips.push({ id: m, name: m, icon: 'book', color: colorPalette[i % colorPalette.length] });
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
      '#1e6ce8',
      '#0c4cb8',
      '#2563eb',
      '#0891b2',
      '#0284c7',
      '#059669',
      '#0d9488',
      '#16a34a',
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
    const colors = ['#34d399', '#22c55e', '#3b82f6', '#1e6ce8', '#0c4cb8'];
    return colors[(d || 1) - 1] || '#94a3b8';
  }

  getMyProgress(courseId: string): number {
    const found = this.myCourses().find(c => c.courseId === courseId);
    return found?.progress || 0;
  }

  isEnrolled(courseId: string): boolean {
    return this.myCourses().some(c => c.courseId === courseId);
  }
}
